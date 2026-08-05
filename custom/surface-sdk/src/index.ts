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

/** The runtime registry plugins dock into (window.calloraSurface). */
export interface SurfaceRegistry {
  readonly views: SurfaceView[]
  registerView(view: SurfaceView): void
  readonly contextChannel: SurfaceContextChannel
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
