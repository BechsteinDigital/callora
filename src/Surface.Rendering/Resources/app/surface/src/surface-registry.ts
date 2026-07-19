import { markRaw, reactive, type Component } from 'vue'

/**
 * A view a plugin contributes to the surface. The runtime ships NO views of its own
 * (the grundgerüst is empty, like a shop framework without a shop) — every concrete
 * surface comes from a plugin registering here.
 */
export interface SurfaceView {
  /** Stable id, unique per surface; a second registration with the same id is ignored. */
  id: string
  /** The Vue component rendered for this view. Receives the SurfaceContext as a prop. */
  component: Component
  /** Ascending render order; unset sorts as 0. */
  order?: number
}

/**
 * The dock point plugins register against (exposed as window.calloraSurface). Kept
 * intentionally small: register a view, read the current views. Richer extension
 * kinds (widgets, slots, routing) are added when a real plugin needs them, not before.
 */
export interface SurfaceRegistry {
  readonly views: SurfaceView[]
  registerView(view: SurfaceView): void
}

export function createSurfaceRegistry(): SurfaceRegistry {
  const views = reactive<SurfaceView[]>([])

  return {
    views,
    registerView(view: SurfaceView): void {
      if (views.some((existing) => existing.id === view.id)) {
        return
      }

      // markRaw: a Vue component definition must not be turned into a reactive proxy.
      views.push({ ...view, component: markRaw(view.component) })
      views.sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
    },
  }
}
