import * as Vue from 'vue'
import './styles/tokens.scss'
import { createSurfaceRegistry, type SurfaceRegistry } from './surface-registry'
import { mountSurface } from './mount'
import { resolveSurfaceContext } from './surface-context'
import { loadSurfacePlugins } from './plugin-loader'

declare global {
  interface Window {
    /** The single shared Vue instance; plugin bundles keep vue external and use this. */
    CalloraVue?: typeof Vue
    /** The dock point plugins register their surface views against. */
    calloraSurface?: SurfaceRegistry
  }
}

// Re-expose the runtime's Vue so plugin bundles (vue external → CalloraVue global)
// run components inside this same instance instead of shipping their own.
window.CalloraVue = Vue

const registry = window.calloraSurface ?? createSurfaceRegistry()
window.calloraSurface = registry

// Mount whichever surface shape the SSR output rendered — whole app (#callora-app)
// and/or islands (data-callora-island). Absent both, the runtime does nothing.
mountSurface(registry)

// Then load the workspace's plugin bundles; they register into calloraSurface and the
// reactive mounts pick them up. Loading is fire-and-forget and self-tolerant, so it
// never blocks or breaks the already-mounted shell.
const contextRoot =
  document.getElementById('callora-app') ?? document.querySelector<HTMLElement>('[data-workspace]')
if (contextRoot) {
  void loadSurfacePlugins(resolveSurfaceContext(contextRoot))
}
