import { computed, createApp, defineComponent, h } from 'vue'
import App from './App.vue'
import { readSurfaceContext, resolveSurfaceContext, type SurfaceContext } from './surface-context'
import { isSurfaceViewVisible, type SurfaceRegistry } from './surface-registry'

/**
 * Mounts the surface runtime in whichever mode the SSR output calls for — both are
 * driven by the same registry and the same shared Vue, and the two scans are
 * independent (a surface uses one or the other):
 *
 *  - APP mode: a single #callora-app root → the whole surface is one Vue app that
 *    renders every registered view. This is what the built-in SpaRoot emits, for
 *    app-surfaces (Communication/Dialer).
 *  - ISLANDS mode: [data-callora-island="<viewId>"] placeholders inside server-
 *    rendered content → Vue mounts only the matching registered view into each,
 *    for content-surfaces (Shopware-storefront-analog progressive enhancement).
 */
export function mountSurface(registry: SurfaceRegistry, doc: Document = document): void {
  const appRoot = doc.getElementById('callora-app')
  if (appRoot) {
    createApp(App, { context: readSurfaceContext(appRoot), registry }).mount(appRoot)
  }

  const islands = doc.querySelectorAll<HTMLElement>('[data-callora-island]')
  islands.forEach((island) => {
    const viewId = island.dataset.calloraIsland
    if (!viewId) {
      return
    }

    createApp(islandHost(registry, viewId, resolveSurfaceContext(island))).mount(island)
  })
}

/**
 * A one-slot host that renders the registered view whose id matches the island — or
 * nothing until it registers. The lookup is reactive, so a view contributed after the
 * island mounted (a plugin bundle loading later) appears without re-scanning the DOM.
 */
function islandHost(registry: SurfaceRegistry, viewId: string, context: SurfaceContext) {
  return defineComponent({
    name: 'CalloraSurfaceIsland',
    setup() {
      const view = computed(() =>
        registry.views.find(
          (candidate) =>
            candidate.id === viewId && isSurfaceViewVisible(candidate, context.surfaceKey),
        ),
      )
      return () => (view.value ? h(view.value.component, { context }) : null)
    },
  })
}
