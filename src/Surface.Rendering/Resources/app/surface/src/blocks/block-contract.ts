import type { Component } from 'vue'

/**
 * What a block's control is bound to.
 *
 * `context` is the one the comparable tools do not have. A value bound to a versioned
 * context key updates when that context changes — an incoming call, a selected lead —
 * without anyone writing realtime code. Framer, Webflow and Shopware all resolve their
 * bindings once, at request time, and freeze the result.
 *
 * `scope` is optional and normally omitted: the resolver decides whether a key comes
 * from this surface or from another one the visitor has open (design §5.3).
 */
export type Binding<T> =
  | { source: 'static'; value: T }
  | { source: 'context'; key: string; scope?: 'local' | 'shared'; path?: string }
  | { source: 'inherit' }
  | { source: 'default' }

/**
 * Control types that shape APPEARANCE. Closed on purpose, and the reason the editor can
 * promise that a composed page still looks like the product: each one picks a --cal-*
 * role or step, never a free value. A plugin cannot contribute here — a free colour
 * picker would undo the guardrail in one registration.
 */
export type AppearanceControlType = 'colorToken' | 'spacingToken' | 'typeToken' | 'variant'

/** Control types that carry content. */
export type ContentControlType =
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

/** Control types that bind to a source rather than hold a value. */
export type SourceControlType = 'context' | 'query'

/**
 * Takes other blocks. A control type rather than a separate `slots:` field (Webflow's
 * idea, cleaner than Shopware's): nesting then falls out of the same mechanism instead
 * of needing one of its own — and with it, Shopware's block/element split disappears.
 * What Shopware calls an element is a block sitting in another block's slot.
 */
export type StructureControlType = 'slot'

export type KnownControlType =
  | ContentControlType
  | SourceControlType
  | AppearanceControlType
  | StructureControlType

/**
 * The type list is open: a plugin can contribute a phone-number picker or an agent
 * selector (design §4.2). `string & {}` keeps autocompletion for the known types while
 * still accepting a contributed one — a plain `string` would collapse the union and
 * lose it.
 */
export type ControlType = KnownControlType | (string & {})

/** One option of a `select` control. */
export interface ControlOption {
  value: string
  label: string
}

/**
 * A control as the editor renders it. The panel is generated from this — there is no
 * second place where a block describes its settings.
 */
export interface BlockControl<T = unknown> {
  type: ControlType
  label: string
  description?: string
  default?: T
  required?: boolean
  /** Panel grouping, as in Webflow — controls with the same group render together. */
  group?: string
  min?: number
  max?: number
  options?: readonly ControlOption[]
  /**
   * When this control is shown. Framer's `hidden` inverted: stating when something IS
   * relevant reads the way the author thinks about it, and a missing predicate then
   * means "always", not "never".
   */
  visibleWhen?: (values: Readonly<Record<string, unknown>>) => boolean
  /**
   * Marks the value as one the visitor must not read in view-source — an API key, an
   * internal id (design §7.5).
   *
   * **This is a declaration of intent, not a guarantee the render path currently keeps.**
   * Do not put a secret behind it today: `SurfaceCompositionRenderer` accepts a set of
   * confidential controls and would filter them out of `data-callora-props`, but nothing
   * supplies that set — there is no server-side block description to read it from, so
   * `SurfaceRenderEndpoints` constructs the renderer without it and every value ships as
   * written.
   *
   * The wording used to promise the opposite, which is the worse half of the same gap: a
   * block author read it and had no reason to doubt it, while the C# side had said plainly
   * that it was unwired all along.
   *
   * Once a block description exists server-side and is wired into that parameter, this
   * becomes a promise and this note goes — `TheConfidentialFlagDoesNotPromiseMoreThanItKeeps`
   * fails if the two sides drift apart again in either direction.
   */
  confidential?: boolean
}

export type BlockControls = Readonly<Record<string, BlockControl>>

/** Which surfaces a block may appear on. Blocks are surface-neutral by default. */
export type BlockSurface = 'surface' | 'admin'

/**
 * A block: what the editor offers and what the runtime renders. The id IS the view id
 * and the island attribute — one identity, so a block placed in the editor and a view
 * rendered server-side are the same thing rather than two registries to keep in step.
 */
export interface BlockDefinition {
  id: string
  label: string
  description?: string
  /**
   * A free string with its own registration point. Shopware's closed XSD enum is the
   * mistake not to repeat: a plugin that invents a category should not need a change to
   * the host to have it appear.
   */
  category: string
  /** Absent means every surface. */
  surfaces?: readonly BlockSurface[]
  /** Versioned context keys this block reads — the editor warns when none supplies them. */
  requires?: readonly string[]
  /** Versioned context keys this block publishes. */
  provides?: readonly string[]
  controls?: BlockControls
  component: Component
  /**
   * What the editor's block picker shows before the block is placed. Optional: without
   * it the picker renders the real component, which is honest but can be slow for a
   * block that fetches.
   */
  preview?: Component
  /** Ascending render order among views in the same slot. */
  order?: number
  icon?: string
}

/** A category in the editor's block picker. */
export interface BlockCategory {
  id: string
  label: string
  icon?: string
  /** Ascending; unset sorts as 0. */
  order?: number
}

/**
 * Appearance types are reserved — see {@link AppearanceControlType}. Exported so both
 * the registry and its test name the same set.
 */
export const APPEARANCE_CONTROL_TYPES: readonly AppearanceControlType[] = [
  'colorToken',
  'spacingToken',
  'typeToken',
  'variant',
]
