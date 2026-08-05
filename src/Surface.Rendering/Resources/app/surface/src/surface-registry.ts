import { markRaw, reactive, type Component } from 'vue'

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

export function isSurfaceViewVisible(view: SurfaceView, surfaceKey: string): boolean {
  return (
    !view.surfaceKeys ||
    view.surfaceKeys.length === 0 ||
    view.surfaceKeys.some((candidate) => candidate === surfaceKey)
  )
}
