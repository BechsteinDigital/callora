import { computed, createApp, defineComponent, h, type App as VueApp } from 'vue'
import App from './App.vue'
import { bundlesSettled } from './bundle-readiness'
import { reportClientError } from './client-error-reporting'
import { readSurfaceContext, resolveSurfaceContext, type SurfaceContext } from './surface-context'
import {
  isSurfaceViewVisible,
  type SurfaceRegistry,
  type SurfaceViewParams,
} from './surface-registry'

// Vue fängt Fehler aus Rendern, Lifecycle und Watchern selbst ab; über window.onerror kommen sie
// nie. Auf einer Fläche zählt das doppelt: Was hier wirft, ist der Code eines Plugins in der Seite
// eines Kunden, und ohne diesen Handler bleibt davon eine leere Stelle und sonst nichts (#294).
function reporting<T>(app: VueApp<T>): VueApp<T> {
  app.config.errorHandler = (error) => reportClientError(error)
  return app
}

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
    reporting(createApp(App, { context: readSurfaceContext(appRoot), registry })).mount(appRoot)
  }

  const islands = doc.querySelectorAll<HTMLElement>('[data-callora-island]')
  islands.forEach((island) => {
    const viewId = island.dataset.calloraIsland
    if (!viewId) {
      return
    }

    reporting(
      createApp(islandHost(registry, viewId, resolveSurfaceContext(island), readIslandParams(island))),
    ).mount(island)
  })
}

/**
 * A one-slot host that renders the registered view whose id matches the island — or
 * nothing until it registers. The lookup is reactive, so a view contributed after the
 * island mounted (a plugin bundle loading later) appears without re-scanning the DOM.
 */
function islandHost(
  registry: SurfaceRegistry,
  viewId: string,
  context: SurfaceContext,
  params: SurfaceViewParams,
) {
  return defineComponent({
    name: 'CalloraSurfaceIsland',
    setup() {
      const view = computed(() =>
        registry.views.find(
          (candidate) =>
            candidate.id === viewId && isSurfaceViewVisible(candidate, context.surfaceKey),
        ),
      )
      // Drei Zustände, nicht zwei. Solange die Bundles unterwegs sind, bleibt die Insel leer —
      // ein Platzhalter, der eine Sekunde später wieder verschwindet, ist schlimmer als nichts.
      // Ist der Versuch vorbei und die Ansicht fehlt weiterhin, sagt die Insel das: Ein leeres
      // div ist die einzige Variante, die weder dem Besucher noch dem Betrieb etwas sagt (#296).
      let reported = false

      return () => {
        if (view.value) {
          return h(view.value.component, { context, params })
        }
        if (!bundlesSettled.value) {
          return null
        }

        if (!reported) {
          reported = true
          reportClientError(new Error(`Surface island "${viewId}" has no registered view.`))
        }

        return h('p', { class: 'cal-island-unavailable' }, 'Dieser Bereich ist gerade nicht verfügbar.')
      }
    },
  })
}

/**
 * Instance parameters the server put on the island. They arrive as a separate `params`
 * prop rather than spread onto the component, so a template can name a parameter
 * freely without colliding with `context` or leaking into the element's attributes.
 *
 * A malformed payload yields no parameters instead of failing the mount: a broken
 * attribute must not cost the visitor the whole view.
 */
export function readIslandParams(island: HTMLElement): SurfaceViewParams {
  const raw = island.dataset.calloraProps
  if (!raw) {
    return {}
  }

  try {
    const parsed: unknown = JSON.parse(raw)
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed)
      ? (parsed as SurfaceViewParams)
      : {}
  } catch {
    return {}
  }
}
