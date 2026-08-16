import { computed, ref } from 'vue'
import { useAuthStore } from '@/core/auth/authStore'
import { workspacesApi, type Workspace } from '@/modules/workspaces/workspacesApi'

// The globally-shared active-workspace context (module singleton, like the auth
// store). A workspace-bound admin has a FIXED workspace (from their token); an
// operator picks one here, and every workspace-scoped view reads the same value —
// so the topbar switcher replaces the former per-view pickers.
const STORAGE_KEY = 'callora.activeWorkspace'

const workspaces = ref<Workspace[]>([])
const selected = ref('')
let loadPromise: Promise<void> | null = null

function readStored(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY)
  } catch {
    return null
  }
}

function writeStored(key: string): void {
  try {
    localStorage.setItem(STORAGE_KEY, key)
  } catch {
    // Private-mode / disabled storage: the in-memory selection still works.
  }
}

// Loads the operator's workspace list once and restores the persisted selection
// (falling back to the first workspace). A fixed admin never needs the list.
//
// Fällt die Auswahl auf den ersten Workspace zurück, wird sie GESPEICHERT und die Shell
// einmal neu geladen. Ohne das war ein frisch installiertes System dauerhaft ohne
// Plugin-Oberfläche: Der Bootstrap fragt die Plugin-Kette mit dem gespeicherten Workspace
// an, hier wurde die Rückfallauswahl aber nur in den Speicher geschrieben. Beim nächsten
// Start las der Bootstrap wieder nichts, die Kette blieb leer, und jede Plugin-Seite
// behauptete, das Plugin liefere keine Oberfläche. Dieselbe Begründung wie bei setActive:
// Ein geladenes Skript lässt sich nicht entladen, also ist ein Neuladen die einzige
// Antwort, die nicht halb richtig ist.
//
// Keine Schleifengefahr: Gespeichert wird VOR dem Neuladen, der nächste Start findet den
// Wert und die Bedingung greift nicht mehr.
function ensureLoaded(fixed: string | null, reload: () => void): Promise<void> {
  if (fixed) {
    return Promise.resolve()
  }
  if (!loadPromise) {
    loadPromise = (async () => {
      workspaces.value = await workspacesApi.list()
      const stored = readStored()
      const resolved =
        stored && workspaces.value.some((w) => w.workspaceKey === stored)
          ? stored
          : (workspaces.value[0]?.workspaceKey ?? '')
      selected.value = resolved

      if (resolved !== '' && resolved !== stored) {
        writeStored(resolved)
        reload()
      }
    })().catch((error: unknown) => {
      // Never cache a failed load — a later ensure() (e.g. a remount) may retry.
      loadPromise = null
      throw error
    })
  }
  return loadPromise
}

/**
 * The persisted workspace selection, readable before the context is initialised.
 * The bootstrap needs it synchronously: the plugin UI chain is requested before any
 * component mounts, and a platform operator carries no workspace in their token.
 */
export function readStoredWorkspace(): string | null {
  const stored = readStored()
  return stored && stored.trim() !== '' ? stored : null
}

export function useWorkspaceContext() {
  const ctx = useAuthStore().context
  // A blank token workspaceKey means "operator" — treat it as not fixed.
  const fixedWorkspace = computed(() => {
    const key = ctx.value?.workspaceKey
    return key && key.trim() !== '' ? key : null
  })
  // The effective workspace every view should scope to.
  const activeWorkspace = computed(() => fixedWorkspace.value ?? selected.value)
  // The switcher is only meaningful for an operator with a choice.
  const canSwitch = computed(() => !fixedWorkspace.value && workspaces.value.length > 0)

  /**
   * Switches the active workspace.
   *
   * Reloads the shell afterwards, because plugin admin bundles are chained per workspace: the
   * bundles currently in the document belong to the previous one, and a loaded script cannot be
   * unloaded. Keeping them while switching would show a plugin's interface in a workspace it is
   * not assigned to — exactly the state the chain exists to prevent. A reload is the only
   * outcome that is not half-right.
   *
   * The reload is injectable so a test can observe it without navigating.
   */
  function setActive(key: string, reload: () => void = () => window.location.reload()): void {
    if (key === activeWorkspace.value) {
      return
    }
    selected.value = key
    writeStored(key)
    reload()
  }

  return {
    workspaces,
    activeWorkspace,
    fixedWorkspace,
    canSwitch,
    ensure: (reload: () => void = () => window.location.reload()) =>
      ensureLoaded(fixedWorkspace.value, reload),
    setActive,
  }
}

// Setzt das Modul-Singleton zurück, samt des gespeicherten Schlüssels: Er überlebt sonst auch das
// Neuladen und trüge die Auswahl der vorigen Sitzung in die nächste. Aufgerufen aus `endSession`
// — und aus Tests.
export function resetWorkspaceContext(): void {
  workspaces.value = []
  selected.value = ''
  loadPromise = null
  try {
    localStorage.removeItem(STORAGE_KEY)
  } catch {
    // ignore
  }
}
