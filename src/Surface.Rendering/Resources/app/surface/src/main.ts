import * as Vue from 'vue'
import './styles/tokens.scss'
import { createSurfaceRegistry, type SurfaceRegistry } from './surface-registry'
import { mountSurface } from './mount'

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
