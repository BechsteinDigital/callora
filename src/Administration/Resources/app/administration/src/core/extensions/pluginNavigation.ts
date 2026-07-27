import { ref } from 'vue'
import { apiFetch } from '@/core/http'

// A plugin-contributed admin navigation entry, as served (already permission
// filtered for the current session) by the host at /api/ext/admin/navigation.
// Mirrors HostAdminNavigationItem on the backend.
export interface PluginNavItem {
  readonly pluginId: string
  readonly id: string
  readonly label: string
  readonly to: string
  readonly icon: string | null
  readonly order: number
}

const items = ref<PluginNavItem[]>([])
let loaded = false

// The reactive plugin navigation the sidebar renders below the built-in items,
// so an installed plugin (e.g. Communication) becomes reachable without a shell
// rebuild. The list is authoritative from the server — no client-side gating.
export function usePluginNavigation() {
  return { items }
}

// Fetches the plugin navigation once per session. Navigation is a convenience
// surface: any failure leaves the list empty and never breaks the shell.
export async function loadPluginNavigation(): Promise<void> {
  if (loaded) {
    return
  }
  loaded = true
  try {
    const res = await apiFetch('/api/ext/admin/navigation')
    if (!res.ok) {
      return
    }
    items.value = (await res.json()) as PluginNavItem[]
  } catch {
    items.value = []
  }
}

// Test/hot-reload aid — clears the cache so the next load re-fetches.
export function resetPluginNavigation(): void {
  items.value = []
  loaded = false
}
