import * as Vue from 'vue'
import './styles/tokens.scss'
import { type SurfaceRegistry } from './surface-registry'
import { mountSurface } from './mount'
import { resolveSurfaceContext } from './surface-context'
import { ensureSurfaceRegistry, loadSurfaceBundles } from './public/bundles'
import { connectSurfaceContextBridge } from './context-bridge'
import { installClientErrorReporting, reportClientError } from './client-error-reporting'
import { markBundlesSettled } from './bundle-readiness'

declare global {
  interface Window {
    /** The single shared Vue instance; plugin bundles keep vue external and use this. */
    CalloraVue?: typeof Vue
    /** The dock point plugins register their surface views against. */
    calloraSurface?: SurfaceRegistry
  }
}

// Zuerst der Melder: Was danach schiefgeht — ein Bundle, das beim Laden wirft, eine Insel, die
// beim Mounten scheitert —, erreicht sonst niemanden. Der Besucher sieht eine kaputte Seite, und
// der Betrieb erfährt davon, wenn ein Kunde anruft (#294).
installClientErrorReporting()

// Re-expose the runtime's Vue so plugin bundles (vue external → CalloraVue global)
// run components inside this same instance instead of shipping their own.
window.CalloraVue = Vue

// The channel inside the registry is bound to the surface this page renders, so read
// the context before creating it rather than after.
const rootElement =
  document.getElementById('callora-app') ?? document.querySelector<HTMLElement>('[data-workspace]')
const rootContext = rootElement ? resolveSurfaceContext(rootElement) : null

// Through the same capability an editor uses (@callora/surface → ensureSurfaceRegistry),
// so there is one way a registry comes into existence rather than two that can diverge.
const registry = ensureSurfaceRegistry(rootContext?.workspaceKey, rootContext?.surfaceKey)

// Mount whichever surface shape the SSR output rendered — whole app (#callora-app)
// and/or islands (data-callora-island). Absent both, the runtime does nothing.
mountSurface(registry)

// Then load the workspace's plugin bundles; they register into calloraSurface and the
// reactive mounts pick them up. Loading is fire-and-forget and self-tolerant, so it
// never blocks or breaks the already-mounted shell.
if (rootContext) {
  void loadSurfaceBundles({
    workspaceKey: rootContext.workspaceKey,
    surfaceKey: rootContext.surfaceKey,
  })
    // Auch im Fehlerfall: Ein Ladefehler ist genau der Fall, für den eine Insel ihren Platzhalter
    // zeigen soll. Wer nur beim Erfolg markiert, lässt sie ewig auf etwas warten, das nicht kommt.
    .catch((error: unknown) => reportClientError(error))
    .finally(markBundlesSettled)

  // And open the realtime bridge, so a server-side event reaches the views that declared
  // they need it. Non-blocking and self-tolerant like the loader: a surface renders and
  // stays usable whether or not the socket ever connects — it just stays as dynamic as a
  // page without one.
  connectSurfaceContextBridge(registry.contextChannel)
} else {
  // Ohne #callora-app und ohne [data-workspace] gibt es keinen Kontext — es wird nichts geladen
  // und die Bridge bleibt zu. Das ist ein Befund und kein Zustand, in dem man wartet: Die Inseln
  // zeigen sofort, dass hier nichts kommt, und der Betrieb erfährt warum.
  markBundlesSettled()
  reportClientError(
    new Error(
      'Surface runtime found no context: neither #callora-app nor [data-workspace] is present. ' +
        'No plugin bundles were loaded and the context bridge stayed closed.',
    ),
  )
}
