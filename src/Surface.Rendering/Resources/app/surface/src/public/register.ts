import type { BlockCategory, BlockDefinition } from '../blocks/block-contract'
import type { SurfaceRegistry } from '../surface-registry'
import type { SurfaceView } from '../surface-registry'

/**
 * How a plugin docks into the surface runtime.
 *
 * These functions live in the runtime rather than in a separate SDK package, so the
 * contract is declared once. It used to be twice — the runtime was private, so the SDK
 * restated every type it described, and the two could drift without anything noticing.
 *
 * Every function is a no-op with a warning when the runtime is absent, never a throw. A
 * plugin is a guest in a shell it does not own; breaking that shell because it loaded
 * early is never the right trade.
 */

declare global {
  interface Window {
    /** The single shared Vue instance the runtime exposes; plugins keep vue external. */
    CalloraVue?: typeof import('vue')
    /** The runtime registry; present once the surface runtime has initialised. */
    calloraSurface?: SurfaceRegistry
  }
}

function registry(what: string): SurfaceRegistry | undefined {
  const found = window.calloraSurface
  if (!found) {
    console.warn(`[callora-surface] surface runtime not initialised; ${what} was not registered.`)
  }

  return found
}

/**
 * Registers a view. The runtime initialises its registry before loading any plugin — the
 * chain loader runs after mount — so by the time a plugin executes, the registry is there.
 */
export function registerSurfaceView(view: SurfaceView): void {
  registry(`view "${view.id}"`)?.registerView(view)
}

/**
 * Registers a block: renderable immediately (the registry registers its view alongside)
 * and offered in the editor's picker once that exists.
 *
 * Register its category too, unless the host already defines it. A block whose category
 * nobody registered still works — it just appears unnamed, which is better than
 * disappearing because two plugins loaded in the wrong order.
 */
export function registerBlock(block: BlockDefinition): void {
  registry(`block "${block.id}"`)?.blocks.registerBlock(block)
}

/** Registers a category for the editor's block picker. Any id is allowed. */
export function registerBlockCategory(category: BlockCategory): void {
  registry(`category "${category.id}"`)?.blocks.registerBlockCategory(category)
}

/**
 * Registers a control type this plugin implements — a phone-number picker, an agent
 * selector. The appearance types (colorToken, spacingToken, typeToken, variant) are
 * reserved and refused: they pick from --cal-* and nothing else, and a free colour
 * picker contributed here would undo that guarantee in one registration.
 */
export function registerControlType(type: string): void {
  registry(`control type "${type}"`)?.blocks.registerControlType(type)
}
