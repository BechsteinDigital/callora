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
function ensureLoaded(fixed: string | null): Promise<void> {
  if (fixed) {
    return Promise.resolve()
  }
  if (!loadPromise) {
    loadPromise = (async () => {
      workspaces.value = await workspacesApi.list()
      const stored = readStored()
      selected.value =
        stored && workspaces.value.some((w) => w.workspaceKey === stored)
          ? stored
          : (workspaces.value[0]?.workspaceKey ?? '')
    })()
  }
  return loadPromise
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

  function setActive(key: string): void {
    selected.value = key
    writeStored(key)
  }

  return {
    workspaces,
    activeWorkspace,
    fixedWorkspace,
    canSwitch,
    ensure: () => ensureLoaded(fixedWorkspace.value),
    setActive,
  }
}

// Resets the module singleton — for tests only.
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
