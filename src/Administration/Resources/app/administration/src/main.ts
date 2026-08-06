import { createApp } from 'vue'
import App from './App.vue'
// Tokens and the global base layer. base.scss pulls in tokens.scss itself; layout.scss holds
// this shell's own chrome measurements (sidebar, topbar), which no other surface has.
import '@/core/design/base.scss'
import '@/core/design/layout.scss'
import { router } from '@/app/router'
import { initTheme } from '@/core/design/theme'
import { loadPluginExtensions } from '@/core/extensions/loader'
import { readStoredWorkspace } from '@/core/workspace/workspaceContext'

// Load plugin admin UI (slots/hooks/service overrides register against the global
// API) before mounting, so a plugin's contributions are present on first render.
// A missing chain or a broken bundle is tolerated and never blocks the shell.
async function bootstrap(): Promise<void> {
  // Adopts the persisted colour-scheme choice and starts following the system
  // signal. The inline script in index.html already set the attribute to avoid a
  // flash; this takes over the reactive side.
  initTheme()
  try {
    // A workspace-bound session needs no key — the server resolves the bound one and
    // ignores anything we send. A platform operator carries none in their token, so the
    // persisted selection is passed; without it the server has no workspace to chain for.
    await loadPluginExtensions({ workspaceKey: readStoredWorkspace() ?? undefined })
  } catch {
    // Plugin loading must never prevent the shell itself from mounting.
  }
  createApp(App).use(router).mount('#app')
}

void bootstrap()
