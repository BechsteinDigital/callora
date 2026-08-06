import type { Component } from 'vue'

/**
 * The Callora surface plugin contract. A surface plugin is a Vue bundle (built with
 * the {@link ./vite-preset calloraSurfacePlugin} preset — Vue external, resolved from
 * the runtime's window.CalloraVue) that registers views against the runtime. These
 * types mirror the surface runtime's registry; the runtime is the implementation, this
 * is the public contract plugin authors compile against.
 */

/** Read-only context the runtime hands each view (which workspace/surface renders). */
export interface SurfaceContext {
  workspaceKey: string
  surfaceKey: string
  caller: SurfaceCaller
}

/** Who a surface request belongs to. Stable identity is issuer + subjectId. */
export interface SurfaceSubject {
  issuer: string
  subjectId: string
}

/**
 * Who is using the surface. A caller always exists: between anonymous and logged in
 * sits the recognised guest, which is what a cart or a multi-step form hangs off.
 * The two states are a discriminated union so code cannot mistake the presence of a
 * subject for authentication.
 */
export type SurfaceCaller =
  | { state: 'guest'; subject: SurfaceSubject }
  | {
      state: 'authenticated'
      subject: SurfaceSubject
      displayName: string
      claims: Record<string, string[]>
    }

/**
 * Instance parameters an island carries: what the SSR template passed at the slot's
 * call site, so an embedded view can point at a concrete lead, room or appointment
 * instead of deriving everything from the URL.
 */
export type SurfaceViewParams = Readonly<Record<string, unknown>>

/** A view a plugin contributes — rendered as the whole app or into a matching island. */
export interface SurfaceView {
  /** Stable, unique id. Also the value of data-callora-island for island mounts. */
  id: string
  /**
   * The Vue component. Receives the SurfaceContext as a `context` prop and the
   * island's instance parameters as a `params` prop.
   */
  component: Component
  /** Ascending render order in app mode; unset sorts as 0. */
  order?: number
  /**
   * Optional allowlist of surface keys this view belongs to. Omit it for a
   * workspace-wide contribution.
   */
  surfaceKeys?: readonly string[]
}

/** Whether a context key tolerates more than one publisher at a time. */
export type SurfaceContextCardinality = 'single' | 'multiple'

/** What a publisher declares before it may write to a context key. */
export interface SurfaceContextDescriptor {
  /** Namespaced and versioned, for example `crm.lead-selection/v1`. */
  key: string
  /** Plugin claiming the key, so diagnostics can name it. */
  publisherPluginId: string
  /** Defaults to `single`: most workplace contexts have exactly one owner. */
  cardinality?: SurfaceContextCardinality
  /** Optional value check; a rejected value is not published. */
  validate?: (value: unknown) => boolean
}

/** A registered publisher's handle. Disposing it releases the key. */
export interface SurfaceContextPublisher<T = unknown> {
  readonly accepted: boolean
  publish(value: T): void
  clear(): void
  dispose(): void
}

/** What the channel is currently doing, for a diagnostics panel. */
export interface SurfaceContextKeyDiagnostics {
  key: string
  publishers: readonly string[]
  subscriberCount: number
  hasValue: boolean
  rejectedPublishers: readonly string[]
  rejectedValues: number
}

/**
 * The channel islands collaborate over. Two plugins share a documented vocabulary
 * rather than knowing each other: a CRM list publishes `crm.lead-selection/v1`, a
 * phone panel consumes it, neither imports the other.
 *
 * It carries UI state, never authority. Anything that must be enforced goes through
 * an authorised API: a value here came from another script on the same page.
 */
export interface SurfaceContextChannel {
  readonly workspaceKey: string
  readonly surfaceKey: string
  providePublisher<T = unknown>(descriptor: SurfaceContextDescriptor): SurfaceContextPublisher<T>
  read<T = unknown>(key: string): T | undefined
  subscribe<T = unknown>(key: string, handler: (value: T | undefined) => void): () => void
  diagnostics(): readonly SurfaceContextKeyDiagnostics[]
}

// ─────────────────────────────────────────────────────────────────────────────
// Blocks
//
// A block is a view with editor metadata — the same component, one identity. Its id
// IS the view id IS the island attribute, so a block placed in the editor and a view
// rendered server-side are the same thing rather than two registries to keep in step.
// ─────────────────────────────────────────────────────────────────────────────

/**
 * What a control is bound to. `source: 'context'` is the one the comparable tools do
 * not have: the value comes from a versioned context key and updates when that context
 * changes — an incoming call, a selected lead — with no realtime code. Framer, Webflow
 * and Shopware resolve their bindings once and freeze the result.
 *
 * `scope` is normally omitted; the resolver decides whether a key comes from this
 * surface or another one the visitor has open.
 */
export type Binding<T> =
  | { source: 'static'; value: T }
  | { source: 'context'; key: string; scope?: 'local' | 'shared'; path?: string }
  | { source: 'inherit' }
  | { source: 'default' }

/**
 * Control types that shape APPEARANCE. Closed: each picks a --cal-* role or step, never
 * a free value. Contributing one is refused — a free colour picker would undo the
 * guardrail that lets the editor promise a composed page still looks like the product.
 */
export type AppearanceControlType = 'colorToken' | 'spacingToken' | 'typeToken' | 'variant'

export type KnownControlType =
  | 'text'
  | 'richText'
  | 'number'
  | 'toggle'
  | 'select'
  | 'list'
  | 'group'
  | 'media'
  | 'link'
  | 'date'
  | 'context'
  | 'query'
  | AppearanceControlType
  /** Takes other blocks — nesting without a separate slots field. */
  | 'slot'

/**
 * Open on purpose: a plugin can contribute a phone-number picker or an agent selector.
 * The `string & {}` keeps autocompletion for the known types, which a plain `string`
 * would collapse.
 */
// eslint-disable-next-line @typescript-eslint/ban-types
export type ControlType = KnownControlType | (string & {})

export interface ControlOption {
  value: string
  label: string
}

/** A control as the editor renders it. The settings panel is generated from this. */
export interface BlockControl<T = unknown> {
  type: ControlType
  label: string
  description?: string
  default?: T
  required?: boolean
  /** Panel grouping — controls sharing a group render together. */
  group?: string
  min?: number
  max?: number
  options?: readonly ControlOption[]
  /**
   * When this control is shown. Stated positively, unlike Framer's `hidden`: a missing
   * predicate then means "always", which is the safer default to forget.
   */
  visibleWhen?: (values: Readonly<Record<string, unknown>>) => boolean
  /** Keeps the value out of the delivered markup — an api key, an internal id. */
  confidential?: boolean
}

export type BlockSurface = 'surface' | 'admin'

/** A block: what the editor offers and what the runtime renders. */
export interface BlockDefinition {
  /** Stable id — also the view id and the island attribute. */
  id: string
  label: string
  description?: string
  /** A free string. Register it with registerBlockCategory to give it a name and icon. */
  category: string
  /** Absent means every surface. */
  surfaces?: readonly BlockSurface[]
  /** Versioned context keys this block reads. */
  requires?: readonly string[]
  /** Versioned context keys this block publishes. */
  provides?: readonly string[]
  controls?: Readonly<Record<string, BlockControl>>
  component: Component
  /** What the picker shows before placement; without it the real component renders. */
  preview?: Component
  order?: number
  icon?: string
}

export interface BlockCategory {
  id: string
  label: string
  icon?: string
  order?: number
}

/** Blocks and their categories, on the runtime registry. */
export interface BlockRegistry {
  readonly blocks: BlockDefinition[]
  readonly categories: BlockCategory[]
  readonly controlTypes: string[]
  registerBlock(block: BlockDefinition): void
  registerBlockCategory(category: BlockCategory): void
  registerControlType(type: string): void
}

/** The runtime registry plugins dock into (window.calloraSurface). */
export interface SurfaceRegistry {
  readonly views: SurfaceView[]
  registerView(view: SurfaceView): void
  readonly contextChannel: SurfaceContextChannel
  readonly blocks: BlockRegistry
}

declare global {
  interface Window {
    /** The single shared Vue instance the runtime exposes; plugins keep vue external. */
    CalloraVue?: typeof import('vue')
    /** The runtime registry; present once the surface runtime has initialised. */
    calloraSurface?: SurfaceRegistry
  }
}

/**
 * Registers a view with the surface runtime. The runtime bundle initialises the
 * registry before it loads any plugin (chain loader runs after mount), so by the time
 * a plugin executes the registry is present. If it is somehow absent the call is a
 * no-op with a warning rather than a crash — a plugin must never break the shell.
 */
export function registerSurfaceView(view: SurfaceView): void {
  const registry = window.calloraSurface
  if (!registry) {
    console.warn(
      `[callora-surface-sdk] surface runtime not initialised; view "${view.id}" was not registered.`,
    )
    return
  }

  registry.registerView(view)
}

/**
 * Registers a block. It becomes renderable immediately — the registry registers the
 * view alongside it — and appears in the editor's picker once that exists.
 *
 * Register the category too, unless it is one the host already defines: a block whose
 * category nobody registered still works, but shows up unnamed.
 */
export function registerBlock(block: BlockDefinition): void {
  const registry = window.calloraSurface
  if (!registry) {
    console.warn(
      `[callora-surface-sdk] surface runtime not initialised; block "${block.id}" was not registered.`,
    )
    return
  }

  registry.blocks.registerBlock(block)
}

/** Registers a category for the editor's block picker. Any id is allowed. */
export function registerBlockCategory(category: BlockCategory): void {
  const registry = window.calloraSurface
  if (!registry) {
    console.warn(
      `[callora-surface-sdk] surface runtime not initialised; category "${category.id}" was not registered.`,
    )
    return
  }

  registry.blocks.registerBlockCategory(category)
}

/**
 * Registers a control type this plugin implements — a phone-number picker, an agent
 * selector. Appearance types (colorToken, spacingToken, typeToken, variant) are
 * reserved and refused: they pick from --cal-* and nothing else.
 */
export function registerControlType(type: string): void {
  const registry = window.calloraSurface
  if (!registry) {
    console.warn(
      `[callora-surface-sdk] surface runtime not initialised; control type "${type}" was not registered.`,
    )
    return
  }

  registry.blocks.registerControlType(type)
}

/**
 * The surface's context channel, or undefined when the runtime has not initialised.
 * Returned rather than thrown for the same reason a missing registry warns instead of
 * crashing: a plugin must never break the shell it is a guest in.
 */
export function surfaceContextChannel(): SurfaceContextChannel | undefined {
  const channel = window.calloraSurface?.contextChannel
  if (!channel) {
    console.warn('[callora-surface-sdk] surface runtime not initialised; no context channel.')
  }

  return channel
}

/** Collects everything a view took from the channel, so one call gives it all back. */
export interface SurfaceContextScope {
  /** Publishes under a key this scope now owns until it is disposed. */
  publish<T = unknown>(descriptor: SurfaceContextDescriptor): SurfaceContextPublisher<T>
  /** Subscribes for as long as this scope lives. */
  subscribe<T = unknown>(key: string, handler: (value: T | undefined) => void): void
  /** Releases every publisher and subscription taken through this scope. */
  dispose(): void
}

/**
 * A scope for one view's use of the channel. Call it in `setup()` and hand `dispose`
 * to `onUnmounted`: a view that leaves the page must not keep a key claimed or keep
 * receiving values into a component that no longer exists.
 */
export function createSurfaceContextScope(): SurfaceContextScope {
  const channel = surfaceContextChannel()
  const publishers: SurfaceContextPublisher[] = []
  const unsubscribes: (() => void)[] = []

  return {
    publish<T>(descriptor: SurfaceContextDescriptor): SurfaceContextPublisher<T> {
      const publisher = channel?.providePublisher<T>(descriptor) ?? {
        accepted: false,
        publish: () => {},
        clear: () => {},
        dispose: () => {},
      }
      publishers.push(publisher as SurfaceContextPublisher)
      return publisher
    },
    subscribe<T>(key: string, handler: (value: T | undefined) => void): void {
      const unsubscribe = channel?.subscribe<T>(key, handler)
      if (unsubscribe) {
        unsubscribes.push(unsubscribe)
      }
    },
    dispose(): void {
      unsubscribes.splice(0).forEach((unsubscribe) => unsubscribe())
      publishers.splice(0).forEach((publisher) => publisher.dispose())
    },
  }
}
