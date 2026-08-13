import { describe, it, expect, beforeEach, vi } from 'vitest'

vi.mock('@/modules/workspaces/workspacesApi', () => ({ workspacesApi: { list: vi.fn() } }))
vi.mock('@/modules/plugins/pluginsApi', () => ({
  pluginsApi: { list: vi.fn() },
  isPluginActive: (state: number) => state === 1,
}))
vi.mock('@/modules/users/usersApi', () => ({ usersApi: { list: vi.fn() } }))
vi.mock('@/core/http', () => ({ apiFetch: vi.fn() }))

import { workspacesApi } from '@/modules/workspaces/workspacesApi'
import { pluginsApi } from '@/modules/plugins/pluginsApi'
import { usersApi } from '@/modules/users/usersApi'
import { apiFetch } from '@/core/http'
import { useOnboarding, shouldAutoRedirect, markAutoShown, resetOnboardingState } from './onboarding'

const wsList = vi.mocked(workspacesApi.list)
const plList = vi.mocked(pluginsApi.list)
const usList = vi.mocked(usersApi.list)
const fetchMock = vi.mocked(apiFetch)

function jsonResponse(data: unknown, ok = true): Response {
  return { ok, json: async () => data } as unknown as Response
}

// Sets the four platform signals; SIP is served through the proxy fetch.
function stubPlatform(opts: { workspaces?: string[]; activePlugin?: boolean; users?: number; sip?: number }): void {
  wsList.mockResolvedValue((opts.workspaces ?? []).map((workspaceKey) => ({ workspaceKey })) as never)
  plList.mockResolvedValue((opts.activePlugin ? [{ state: 1 }] : []) as never)
  usList.mockResolvedValue(Array.from({ length: opts.users ?? 1 }, () => ({})) as never)
  fetchMock.mockResolvedValue(jsonResponse(Array.from({ length: opts.sip ?? 0 }, (_, i) => ({ id: String(i) }))))
}

beforeEach(() => {
  vi.clearAllMocks()
  localStorage.clear()
  resetOnboardingState()
})

describe('onboarding', () => {
  it('derives every step as complete from full platform state', async () => {
    stubPlatform({ workspaces: ['w1'], activePlugin: true, users: 2, sip: 1 })
    const onboarding = useOnboarding()

    await onboarding.loadStatus()

    expect(onboarding.completedCount.value).toBe(4)
    expect(onboarding.isComplete.value).toBe(true)
  })

  it('leaves steps incomplete on a fresh install', async () => {
    stubPlatform({ workspaces: [], activePlugin: false, users: 1, sip: 0 })
    const onboarding = useOnboarding()

    await onboarding.loadStatus()

    expect(onboarding.steps.value.find((s) => s.key === 'workspace')?.done).toBe(false)
    expect(onboarding.completedCount.value).toBe(0)
    // A single operator + no workspace + Communication inactive in this stub.
    expect(fetchMock).not.toHaveBeenCalled() // no workspace → SIP not queried
  })

  it('auto-redirects once on a fresh install, then never again', async () => {
    stubPlatform({ workspaces: [] })
    await useOnboarding().loadStatus()

    expect(shouldAutoRedirect()).toBe(true)
    markAutoShown()
    expect(shouldAutoRedirect()).toBe(false)
  })

  it('does not auto-redirect once a workspace exists', async () => {
    stubPlatform({ workspaces: ['w1'] })
    await useOnboarding().loadStatus()

    expect(shouldAutoRedirect()).toBe(false)
  })

  it('dismiss hides the card and persists', () => {
    const onboarding = useOnboarding()
    expect(onboarding.isDismissed.value).toBe(false)

    onboarding.dismiss()

    expect(onboarding.isDismissed.value).toBe(true)
    expect(localStorage.getItem('callora.onboarding.dismissed')).toBe('1')
  })

  // #291: Ein einmaliger Serverfehler machte aus „unbekannt" eine 0 und schob den Operator in
  // den Erstinstallations-Assistenten — samt Verbrauch des Einmal-Flags.
  it('does not auto-redirect when the workspace list could not be loaded', async () => {
    wsList.mockRejectedValueOnce(new Error('500'))
    plList.mockResolvedValueOnce([])
    usList.mockResolvedValueOnce([])

    const { loadStatus, steps } = useOnboarding()
    await loadStatus()

    // Kein Umleiten: Der Stand ist unbekannt, nicht null Workspaces.
    expect(shouldAutoRedirect()).toBe(false)
    // Und der erste Schritt gilt nicht als offen, nur weil die Abfrage scheiterte.
    expect(steps.value[0].done).toBe(false)
  })
})
