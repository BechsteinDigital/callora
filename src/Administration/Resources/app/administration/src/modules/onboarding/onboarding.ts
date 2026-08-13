import { computed, ref } from 'vue'
import { apiFetch } from '@/core/http'
import { workspacesApi } from '@/modules/workspaces/workspacesApi'
import { usersApi } from '@/modules/users/usersApi'
import { pluginsApi, isPluginActive } from '@/modules/plugins/pluginsApi'

// First-run onboarding for a fresh install: the platform is already provisioned
// (secrets/operator via the console installer + .env), so this guides the operator
// through the remaining setup — it never touches secrets. Step completion is derived
// from real, server-authoritative state; only the "auto-shown once" and "dismissed"
// hints live client-side (localStorage), deliberately per-browser (a cross-device
// backend preference is a later step).
const AUTO_SHOWN_KEY = 'callora.onboarding.autoShown'
const DISMISSED_KEY = 'callora.onboarding.dismissed'

export interface OnboardingStep {
  readonly key: string
  readonly label: string
  readonly description: string
  readonly to: string
  readonly done: boolean
}

// Singleton reactive signals shared by the wizard, the dashboard card and the shell
// auto-redirect — one source of truth, loaded once and refreshed after an action.
const workspaceCount = ref<number | null>(null)
const activePluginCount = ref<number | null>(null)
const sipAccountCount = ref<number | null>(null)
const userCount = ref<number | null>(null)
const firstWorkspaceKey = ref<string | null>(null)
const dismissed = ref(readFlag(DISMISSED_KEY))
// False until the first loadStatus() resolves, so consumers can hold the card back
// until the real state is known (no flash-then-hide when the platform is set up).
const ready = ref(false)

const steps = computed<OnboardingStep[]>(() => [
  {
    key: 'workspace',
    label: 'Ersten Workspace anlegen',
    description: 'Der Einstieg — danach ist die kundenseitige Oberfläche erreichbar.',
    to: '/onboarding',
    done: (workspaceCount.value ?? 0) > 0,
  },
  {
    key: 'plugins',
    label: 'Plugins ansehen & aktivieren',
    description: 'Installierte Plugins verwalten (Communication ist bereits aktiv).',
    to: '/plugins',
    done: (activePluginCount.value ?? 0) > 0,
  },
  {
    key: 'communication',
    label: 'Ersten SIP-Account anlegen',
    description: 'Voice für den neuen Workspace einrichten.',
    to: firstWorkspaceKey.value
      ? `/extensions/communication?workspaceKey=${encodeURIComponent(firstWorkspaceKey.value)}`
      : '/extensions/communication',
    done: (sipAccountCount.value ?? 0) > 0,
  },
  {
    key: 'users',
    label: 'Weiteren Operator einladen',
    description: 'Einen zweiten Admin/Operator anlegen (optional).',
    to: '/users/new',
    done: (userCount.value ?? 0) > 1,
  },
])

const completedCount = computed(() => steps.value.filter((s) => s.done).length)
const isComplete = computed(() => completedCount.value === steps.value.length)

// Zaehlt den Einrichtungsstand. Ein gescheiterter Aufruf hinterlaesst `null` — „unbekannt" —
// und nicht 0.
//
// Vorher machten drei `.catch(() => [])` aus jedem Fehler ein leeres Ergebnis. Ein einmaliger
// 500er auf /api/workspaces setzte damit workspaceCount auf 0, shouldAutoRedirect() wurde wahr,
// und der Operator wurde aus seiner Route in den Erstinstallations-Assistenten geschoben —
// ohne Fehlerhinweis, mit „0 von 4 erledigt" fuer eine eingerichtete Installation. Nebenbei
// verbrauchte markAutoShown() dabei das Einmal-Flag (#291).
async function loadStatus(): Promise<void> {
  const [workspaces, plugins, users] = await Promise.all([
    workspacesApi.list().catch(() => null),
    pluginsApi.list().catch(() => null),
    usersApi.list().catch(() => null),
  ])
  workspaceCount.value = workspaces?.length ?? null
  firstWorkspaceKey.value = workspaces?.[0]?.workspaceKey ?? null
  activePluginCount.value = plugins ? plugins.filter((p) => isPluginActive(p.state)).length : null
  userCount.value = users?.length ?? null
  sipAccountCount.value = firstWorkspaceKey.value ? await countSipAccounts(firstWorkspaceKey.value) : 0
  ready.value = true
}

async function countSipAccounts(workspaceKey: string): Promise<number> {
  try {
    const res = await apiFetch(
      `/api/ext/admin/plugins/communication/sip-accounts?workspaceKey=${encodeURIComponent(workspaceKey)}`,
    )
    if (!res.ok) {
      return 0
    }
    const data = await res.json()
    return Array.isArray(data) ? data.length : 0
  } catch {
    return 0
  }
}

function readFlag(key: string): boolean {
  try {
    return localStorage.getItem(key) === '1'
  } catch {
    return false
  }
}

function writeFlag(key: string): void {
  try {
    localStorage.setItem(key, '1')
  } catch {
    // Private mode / storage disabled — the hint is best-effort, never fatal.
  }
}

export function useOnboarding() {
  return {
    steps,
    completedCount,
    isComplete,
    isReady: computed(() => ready.value),
    isDismissed: computed(() => dismissed.value),
    loadStatus,
    dismiss(): void {
      dismissed.value = true
      writeFlag(DISMISSED_KEY)
    },
  }
}

// Auto-open the wizard once on a truly fresh install (no workspace yet) and only if
// it has not been auto-shown before. Callers must loadStatus() first.
// `=== 0` und nicht `!workspaceCount.value`: null heisst, dass die Abfrage gescheitert ist, und
// aus einem unbekannten Stand darf kein Assistent folgen — schon gar keiner, der dabei sein
// Einmal-Flag verbraucht.
export function shouldAutoRedirect(): boolean {
  return workspaceCount.value === 0 && !readFlag(AUTO_SHOWN_KEY)
}

export function markAutoShown(): void {
  writeFlag(AUTO_SHOWN_KEY)
}

// Test aid — clears the cached signals so the next loadStatus() re-derives.
export function resetOnboardingState(): void {
  workspaceCount.value = null
  activePluginCount.value = null
  sipAccountCount.value = null
  userCount.value = null
  firstWorkspaceKey.value = null
  dismissed.value = readFlag(DISMISSED_KEY)
  ready.value = false
}
