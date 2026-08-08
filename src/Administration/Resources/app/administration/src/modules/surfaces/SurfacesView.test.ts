import { describe, expect, it, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { mount, flushPromises } from '@vue/test-utils'
import SurfacesView from './SurfacesView.vue'
import type { WorkspaceSurface } from '@/modules/workspaces/workspacesApi'

const { listSurfacesMock, upsertSurfaceMock, removeSurfaceMock, pushMock, routeParams } = vi.hoisted(
  () => {
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const { ref: hoistedRef } = require('vue') as typeof import('vue')
    return {
      listSurfacesMock: vi.fn(),
      upsertSurfaceMock: vi.fn(),
      removeSurfaceMock: vi.fn(),
      pushMock: vi.fn(),
      routeParams: hoistedRef<Record<string, string>>({}),
    }
  },
)

vi.mock('@/modules/workspaces/workspacesApi', () => ({
  SURFACE_ACCESS_MODES: ['Public', 'Authenticated', 'Mixed'] as const,
  SURFACE_ROUTINGS: ['Tree', 'Application'] as const,
  SURFACE_ROUTING_LABELS: { Tree: 'Seitenbaum', Application: 'Anwendung' },
  workspacesApi: {
    listSurfaces: listSurfacesMock,
    upsertSurface: upsertSurfaceMock,
    removeSurface: removeSurfaceMock,
  },
}))
vi.mock('@/core/auth/authStore', () => ({
  useAuthStore: () => ({ context: { value: { permissions: ['*'] } } }),
}))
vi.mock('@/core/workspace/workspaceContext', () => ({
  useWorkspaceContext: () => ({ activeWorkspace: ref('acme') }),
}))
vi.mock('vue-router', () => ({
  useRoute: () => ({ params: routeParams.value }),
  useRouter: () => ({ push: pushMock }),
  RouterLink: { name: 'RouterLink', props: ['to'], template: '<a><slot /></a>' },
}))

function surface(over: Partial<WorkspaceSurface>): WorkspaceSurface {
  return {
    id: over.surfaceKey ?? 'id',
    workspaceKey: 'acme',
    surfaceKey: 'portal',
    displayName: 'Portal',
    surfaceType: 'spa',
    publicBaseUrl: null,
    publicHost: null,
    publicPathPrefix: '/',
    accessMode: 'Mixed',
    routing: 'Tree',
    locale: null,
    templatePluginId: null,
    templateVersion: null,
    themePluginId: null,
    themeVersion: null,
    isActive: true,
    createdAtUtc: '',
    updatedAtUtc: '',
    parentSurfaceKey: null,
    position: 0,
    requiredClaims: null,
    ...over,
  } as WorkspaceSurface
}

beforeEach(() => {
  for (const m of [listSurfacesMock, upsertSurfaceMock, removeSurfaceMock, pushMock]) {
    m.mockReset()
  }
  routeParams.value = {}
})

describe('SurfacesView', () => {
  it('zeigt den Baum eingerückt statt als flache Liste', async () => {
    // Die Einrückung IST die Struktur. Ohne sie wäre nicht erkennbar, dass „Kunden" unter
    // „Portal" liegt — und genau das ist die Frage, wegen der jemand diese Ansicht öffnet.
    listSurfacesMock.mockResolvedValue([
      surface({ surfaceKey: 'portal', displayName: 'Portal' }),
      surface({ surfaceKey: 'kunden', displayName: 'Kunden', parentSurfaceKey: 'portal' }),
    ])

    const wrapper = mount(SurfacesView)
    await flushPromises()

    const rows = wrapper.findAll('.tree__row')
    expect(rows).toHaveLength(2)
    expect(rows[0].attributes('style')).toContain('--depth: 0')
    expect(rows[1].attributes('style')).toContain('--depth: 1')
  })

  it('behält den Baum beim Wechsel zwischen Knoten', async () => {
    // Der Unterschied zu Liste-plus-Detailseite: Wer eine Gliederung bearbeitet, arbeitet an
    // den Beziehungen. Verschwände der Baum beim Anklicken, wäre jeder Wechsel ein Rückweg.
    listSurfacesMock.mockResolvedValue([
      surface({ surfaceKey: 'portal' }),
      surface({ surfaceKey: 'kunden', parentSurfaceKey: 'portal' }),
    ])
    routeParams.value = { surfaceKey: 'kunden' }

    const wrapper = mount(SurfacesView)
    await flushPromises()

    expect(wrapper.findAll('.tree__row')).toHaveLength(2)
    expect(wrapper.find('.detail__title').exists()).toBe(true)
  })

  it('lädt die Flächen genau einmal für den aktiven Workspace', async () => {
    listSurfacesMock.mockResolvedValue([])

    mount(SurfacesView)
    await flushPromises()

    expect(listSurfacesMock).toHaveBeenCalledTimes(1)
    expect(listSurfacesMock).toHaveBeenCalledWith('acme')
  })

  it('legt eine Unterseite unter dem angeklickten Knoten an, nicht als Wurzel', async () => {
    // Das Pluszeichen an einer Zeile meint „hierunter". Landete der neue Knoten als Wurzel,
    // müsste man ihn danach im Formular umhängen — und wer es vergisst, bekommt eine Seite
    // unter einer Adresse, die er nicht wollte.
    listSurfacesMock.mockResolvedValue([surface({ surfaceKey: 'portal' })])

    const wrapper = mount(SurfacesView)
    await flushPromises()
    await wrapper.find('.tree__row button[title="Unterseite anlegen"]').trigger('click')
    await flushPromises()

    const parent = wrapper.find('select[name="parentSurfaceKey"]').element as HTMLSelectElement
    expect(parent.value).toBe('portal')
  })
})
