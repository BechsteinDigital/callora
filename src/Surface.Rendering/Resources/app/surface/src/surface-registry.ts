import { markRaw, reactive, type Component } from 'vue'
import {
  createSurfaceContextChannel,
  type SurfaceContextChannel,
} from './surface-context-channel'
import { createBlockRegistry, type BlockRegistry } from './blocks/block-registry'
import type { BlockDefinition } from './blocks/block-contract'

/**
 * Instance parameters an island carries: what the SSR template passed at the slot's
 * call site, so an embedded view can point at a concrete lead, room or appointment
 * instead of deriving everything from the URL.
 */
export type SurfaceViewParams = Readonly<Record<string, unknown>>

/**
 * A view a plugin contributes to the surface. The runtime ships NO views of its own
 * (the grundgerüst is empty, like a shop framework without a shop) — every concrete
 * surface comes from a plugin registering here.
 */
export interface SurfaceView {
  /** Stable id, unique per surface; a second registration with the same id is ignored. */
  id: string
  /**
   * The Vue component rendered for this view. Receives the SurfaceContext as a
   * `context` prop and the island's instance parameters as a `params` prop.
   */
  component: Component
  /** Ascending render order; unset sorts as 0. */
  order?: number
  /** Optional surface-key allowlist; absent means the view is workspace-wide. */
  surfaceKeys?: readonly string[]
}

/**
 * The dock point plugins register against (exposed as window.calloraSurface). Kept
 * intentionally small: register a view, read the current views. Richer extension
 * kinds (widgets, slots, routing) are added when a real plugin needs them, not before.
 */
export interface SurfaceRegistry {
  readonly views: SurfaceView[]
  registerView(view: SurfaceView): void
  /**
   * The channel islands collaborate over. Bound to this surface: it is created with
   * the page and never shared across surfaces or workspaces.
   */
  readonly contextChannel: SurfaceContextChannel
  /**
   * Blocks and their categories — what the editor offers, and the same components the
   * runtime renders. Registering a block also registers its view, so a block is never
   * a second thing to keep in step with a view.
   */
  readonly blocks: BlockRegistry
}

export function createSurfaceRegistry(
  workspaceKey = 'default',
  surfaceKey = 'default',
): SurfaceRegistry {
  const views = reactive<SurfaceView[]>([])
  const registerView = (view: SurfaceView): void => {
    if (views.some((existing) => existing.id === view.id)) {
      return
    }

    // markRaw: a Vue component definition must not be turned into a reactive proxy.
    views.push({ ...view, component: markRaw(view.component) })
    views.sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
  }

  // A registered block becomes a renderable view under the same id — that id is also
  // the island attribute, so server-rendered placement and editor placement meet.
  const blocks = createBlockRegistry((block: BlockDefinition) =>
    registerView({
      id: block.id,
      component: block.component,
      order: block.order,
      surfaceKeys: undefined,
    }),
  )

  return {
    views,
    blocks,
    contextChannel: createSurfaceContextChannel(workspaceKey, surfaceKey),
    registerView,
  }
}

export function isSurfaceViewVisible(view: SurfaceView, surfaceKey: string): boolean {
  return (
    !view.surfaceKeys ||
    view.surfaceKeys.length === 0 ||
    view.surfaceKeys.some((candidate) => candidate === surfaceKey)
  )
}
