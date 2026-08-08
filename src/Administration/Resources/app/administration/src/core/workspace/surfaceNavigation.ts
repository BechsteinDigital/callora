import { computed, ref, watch } from 'vue'
import { useWorkspaceContext } from './workspaceContext'
import { workspacesApi, type WorkspaceSurface } from '@/modules/workspaces/workspacesApi'

/**
 * Die Flächen des aktiven Workspaces, einmal geladen und geteilt.
 *
 * Modul-Singleton wie der Workspace-Kontext: Die Sidebar und die Flächenansicht zeigen
 * dieselbe Gliederung, und zwei getrennte Ladevorgänge zeigten sie beim Anlegen einer Seite
 * verschieden — die eine mit, die andere ohne.
 */
const surfaces = ref<WorkspaceSurface[]>([])
const loading = ref(false)
const loadedFor = ref<string | null>(null)

async function load(workspaceKey: string): Promise<void> {
  if (!workspaceKey) {
    surfaces.value = []
    loadedFor.value = null
    return
  }

  loading.value = true
  try {
    surfaces.value = await workspacesApi.listSurfaces(workspaceKey)
    loadedFor.value = workspaceKey
  } catch {
    // Die Navigation ist kein Ort für eine Fehlermeldung: Wer die Flächen bearbeiten will,
    // öffnet die Ansicht, und dort wird der Fehler gezeigt. Ein leerer Block ist hier die
    // ehrlichere Antwort als ein roter Kasten in der Seitenleiste.
    surfaces.value = []
    loadedFor.value = null
  } finally {
    loading.value = false
  }
}

export function useSurfaceNavigation() {
  const { activeWorkspace } = useWorkspaceContext()

  watch(
    activeWorkspace,
    (key) => {
      if (key && key !== loadedFor.value && !loading.value) {
        void load(key)
      }
    },
    { immediate: true },
  )

  /** Nur die Wurzeln — der Rest der Gliederung gehört in die Ansicht, nicht ins Menü. */
  const roots = computed(() =>
    surfaces.value
      .filter((surface) => !surface.parentSurfaceKey)
      .sort((a, b) => (a.position ?? 0) - (b.position ?? 0) || a.surfaceKey.localeCompare(b.surfaceKey)),
  )

  return {
    surfaces,
    roots,
    loading,
    reload: () => load(activeWorkspace.value),
  }
}

/** Setzt den Modul-Singleton zurück — nur für Tests. */
export function resetSurfaceNavigation(): void {
  surfaces.value = []
  loading.value = false
  loadedFor.value = null
}
