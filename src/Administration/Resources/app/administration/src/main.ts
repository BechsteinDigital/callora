import { createApp } from 'vue'
import App from './App.vue'
import '@/core/design/base.scss'
import { router } from '@/app/router'
import { initTheme } from '@/core/design/theme'
import { loadPluginExtensions } from '@/core/extensions/loader'

// Load plugin admin UI (slots/hooks/service overrides register against the global
// API) before mounting, so a plugin's contributions are present on first render.
// A missing manifest or a broken bundle is tolerated and never blocks the shell.
async function bootstrap(): Promise<void> {
  // Adopts the persisted colour-scheme choice and starts following the system
  // signal. The inline script in index.html already set the attribute to avoid a
  // flash; this takes over the reactive side.
  initTheme()
  try {
    await loadPluginExtensions()
  } catch {
    // Plugin loading must never prevent the shell itself from mounting.
  }
  createApp(App).use(router).mount('#app')
}

void bootstrap()
