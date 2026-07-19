import * as Vue from 'vue'
import { createApp } from 'vue'
import App from './App.vue'
import './styles/tokens.scss'
import { readSurfaceContext } from './surface-context'
import { createSurfaceRegistry, type SurfaceRegistry } from './surface-registry'

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

// The SSR SurfaceShell renders the mount root; if it is absent (e.g. the shell was
// swapped for a full SSR template), the runtime simply does nothing.
const root = document.getElementById('callora-app')
if (root) {
  const context = readSurfaceContext(root)
  createApp(App, { context, registry }).mount(root)
}
