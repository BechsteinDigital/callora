import { describe, expect, it, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { mount, flushPromises } from '@vue/test-utils'
import SurfacesView from './SurfacesView.vue'
import type { WorkspaceSurface } from '@/modules/workspaces/workspacesApi'
import { registerSurfaceTab, resetSurfaceTabs } from '@/core/extensions/surfaceTabs'

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
  SURFACE_AUTHENTICATIONS: ['Public', 'SurfaceIdentity', 'Administration'] as const,
  SURFACE_AUTHENTICATION_LABELS: { Public: 'Öffentlich', SurfaceIdentity: 'Flächen-Anmeldung', Administration: 'Administration' },
  SURFACE_ROUTINGS: ['Tree', 'Application'] as const,
  SURFACE_ROUTING_LABELS: { Tree: 'Seitenbaum', Application: 'Anwendung' },
  workspacesApi: {
    listSurfaces: listSurfacesMock,
    upsertSurface: upsertSurfaceMock,
    removeSurface: removeSurfaceMock,
    listPlugins: vi.fn().mockResolvedValue([
      { pluginId: 'videoconference', displayName: 'Video Conference', isActive: true, isAssigned: true },
    ]),
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
    authentication: 'Public',
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
    grantedClaims: null,
    ...over,
  } as WorkspaceSurface
}

beforeEach(() => {
  for (const m of [listSurfacesMock, upsertSurfaceMock, removeSurfaceMock, pushMock]) {
    m.mockReset()
  }
  routeParams.value = {}
  resetSurfaceTabs()
})

describe('SurfacesView', () => {
  it('zeigt die Reiter der zugewiesenen App — und nur an ihrer Fläche', async () => {
    // Der Grund für die Bindung an die App-Zuweisung: Über einen gewöhnlichen Slot erschiene
    // „Räume" an JEDER Fläche, auch an einer reinen Inhaltsseite. Nach dem dritten Plugin wäre
    // die Detailansicht unbenutzbar — genau deshalb gibt Shopware Apps nur definierte Slots.
    registerSurfaceTab('rooms', 'Räume', { template: '<div class="rooms-panel" />' }, 0, 'videoconference')
    listSurfacesMock.mockResolvedValue([
      surface({ surfaceKey: 'meet', templatePluginId: 'videoconference' }),
      surface({ surfaceKey: 'start', templatePluginId: null }),
    ])

    routeParams.value = { surfaceKey: 'meet' }
    const withApp = mount(SurfacesView)
    await flushPromises()
    expect(withApp.text()).toContain('Räume')

    routeParams.value = { surfaceKey: 'start' }
    const withoutApp = mount(SurfacesView)
    await flushPromises()
    expect(withoutApp.text()).not.toContain('Räume')
  })

  it('ordnet einen Reiter der App zu, die ihn registriert hat', async () => {
    // Die Zuordnung kommt vom Loader, nicht vom Plugin selbst — sonst könnte ein Plugin seinen
    // Reiter an die Fläche eines anderen hängen.
    registerSurfaceTab('rooms', 'Räume', { template: '<div />' }, 0, 'videoconference')
    listSurfacesMock.mockResolvedValue([surface({ surfaceKey: 'shop', templatePluginId: 'anderes-plugin' })])
    routeParams.value = { surfaceKey: 'shop' }

    const wrapper = mount(SurfacesView)
    await flushPromises()

    expect(wrapper.text()).not.toContain('Räume')
  })

  it('leitet die Adressierung aus der App-Zuweisung ab, statt sie zweimal zu fragen', async () => {
    // Eine Fläche, die einer App gehört, deutet ihre Unterpfade selbst — ein Konferenzraum
    // entsteht zur Laufzeit und kann kein Knoten sein. Zwei Felder für eine Entscheidung hätten
    // sich widersprechen können, und der Widerspruch wäre erst als 404 auf einer echten Adresse
    // aufgefallen.
    listSurfacesMock.mockResolvedValue([
      surface({ surfaceKey: 'meet', templatePluginId: 'videoconference', routing: 'Application' }),
    ])
    routeParams.value = { surfaceKey: 'meet' }

    const wrapper = mount(SurfacesView)
    await flushPromises()

    const app = wrapper.find('select[name="templatePluginId"]').element as HTMLSelectElement
    expect(app.value).toBe('videoconference')

    const routing = wrapper.find('input[name="routing"]').element as HTMLInputElement
    expect(routing.value).toBe('Anwendung')
    expect(routing.disabled).toBe(true)
  })

  it('speichert die Adressierung, die aus der App folgt — nicht die zuvor gespeicherte', async () => {
    // Der Fall, der beide Felder auseinanderhält: Die Fläche liegt als `Tree` in der Datenbank,
    // trägt aber eine App. Zeigte oder speicherte das Formular den gelesenen Wert, bliebe sie
    // ein Baum — und jeder Raum darunter antwortete mit 404, obwohl eine App zugewiesen ist.
    listSurfacesMock.mockResolvedValue([
      surface({ surfaceKey: 'meet', templatePluginId: 'videoconference', routing: 'Tree' }),
    ])
    upsertSurfaceMock.mockResolvedValue({})
    routeParams.value = { surfaceKey: 'meet' }

    const wrapper = mount(SurfacesView)
    await flushPromises()

    expect((wrapper.find('input[name="routing"]').element as HTMLInputElement).value).toBe('Anwendung')

    await wrapper.findAll('button').find((b) => b.text() === 'Speichern')!.trigger('click')
    await flushPromises()

    expect(upsertSurfaceMock).toHaveBeenCalledWith(
      'acme',
      'meet',
      expect.objectContaining({ routing: 'Application', templatePluginId: 'videoconference' }),
    )
  })

  it('macht eine Fläche ohne App zur Inhaltsfläche im Baum', async () => {
    // Gegenprobe zum Test darüber: Dieselbe Ableitung, andere Richtung. Die Fläche liegt als
    // `Application` in der Datenbank, hat aber keine App mehr — dann ist der Baum die Wahrheit.
    listSurfacesMock.mockResolvedValue([
      surface({ surfaceKey: 'start', templatePluginId: null, routing: 'Application' }),
    ])
    upsertSurfaceMock.mockResolvedValue({})
    routeParams.value = { surfaceKey: 'start' }

    const wrapper = mount(SurfacesView)
    await flushPromises()

    expect((wrapper.find('input[name="routing"]').element as HTMLInputElement).value).toBe('Seitenbaum')

    await wrapper.findAll('button').find((b) => b.text() === 'Speichern')!.trigger('click')
    await flushPromises()

    expect(upsertSurfaceMock).toHaveBeenCalledWith(
      'acme',
      'start',
      expect.objectContaining({ routing: 'Tree', templatePluginId: null }),
    )
  })

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

  // #291: Ohne den Fehlerzweig war ein 500er von „keine Flaeche vorhanden" nicht zu
  // unterscheiden — inklusive Angebot, die erste anzulegen.
  it('shows the error instead of the empty state when loading fails', async () => {
    listSurfacesMock.mockRejectedValueOnce(new Error('Serverfehler 500'))

    const wrapper = mount(SurfacesView)
    await flushPromises()

    expect(wrapper.text()).toContain('Serverfehler 500')
    expect(wrapper.text()).not.toContain('Noch keine Fläche')
  })
})
