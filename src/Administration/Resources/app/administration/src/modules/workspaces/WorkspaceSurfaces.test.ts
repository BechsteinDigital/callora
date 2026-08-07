import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import WorkspaceSurfaces from './WorkspaceSurfaces.vue'
import type { WorkspaceSurface } from './workspacesApi'
import { registerHook, resetHooks } from '@/core/extensions/hooks'
import { resetServices } from '@/core/extensions/services'

const { listSurfacesMock, upsertSurfaceMock, removeSurfaceMock } = vi.hoisted(() => ({
  listSurfacesMock: vi.fn(),
  upsertSurfaceMock: vi.fn(),
  removeSurfaceMock: vi.fn(),
}))

vi.mock('./workspacesApi', () => ({
  SURFACE_ACCESS_MODES: ['Public', 'Authenticated', 'Mixed'] as const,
  SURFACE_ROUTINGS: ['Tree', 'Application'] as const,
  SURFACE_ROUTING_LABELS: { Tree: 'Seitenbaum', Application: 'Anwendung' },
  workspacesApi: {
    listSurfaces: listSurfacesMock,
    upsertSurface: upsertSurfaceMock,
    removeSurface: removeSurfaceMock,
  },
}))

// The confirm dialog is a promise-based store now, not window.confirm — mock it so
// each test can decide what the operator answers.
const { confirmMock } = vi.hoisted(() => ({ confirmMock: vi.fn() }))
vi.mock('@/core/feedback/confirm', () => ({ confirm: confirmMock }))

beforeEach(() => {
  confirmMock.mockReset().mockResolvedValue(true)
})

function surface(over: Partial<WorkspaceSurface>): WorkspaceSurface {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    workspaceKey: 'acme',
    surfaceKey: 'default',
    displayName: 'Default',
    surfaceType: 'spa',
    publicBaseUrl: null,
    publicHost: 'acme.example.de',
    publicPathPrefix: '/',
    accessMode: 'Mixed',
    routing: 'Tree',
    locale: null,
    templatePluginId: null,
    templateVersion: null,
    themePluginId: 'acme.theme',
    themeVersion: '2.0.0',
    isActive: true,
    createdAtUtc: '',
    updatedAtUtc: '',
    parentSurfaceKey: null,
    position: 0,
    requiredClaims: null,
    ...over,
  }
}

function mountSurfaces(canManage: boolean) {
  return mount(WorkspaceSurfaces, { props: { workspaceKey: 'acme', canManage } })
}

beforeEach(() => {
  listSurfacesMock.mockReset().mockResolvedValue([surface({})])
  upsertSurfaceMock.mockReset().mockResolvedValue(surface({}))
  removeSurfaceMock.mockReset().mockResolvedValue(undefined)
  resetHooks()
  resetServices()
})

describe('WorkspaceSurfaces', () => {
  it('lists surfaces for the workspace with status and access mode', async () => {
    const wrapper = mountSurfaces(true)
    await flushPromises()

    expect(listSurfacesMock).toHaveBeenCalledWith('acme')
    expect(wrapper.text()).toContain('default')
    expect(wrapper.text()).toContain('Mixed')
    expect(wrapper.text()).toContain('acme.example.de')
    expect(wrapper.text()).toContain('Aktiv')
  })

  it('hides the form and row actions without manage permission', async () => {
    const wrapper = mountSurfaces(false)
    await flushPromises()

    expect(wrapper.find('form.surfaces__form').exists()).toBe(false)
    expect(wrapper.find('.is-danger-ghost').exists()).toBe(false)
  })

  it('creates a surface from the form and reloads', async () => {
    const wrapper = mountSurfaces(true)
    await flushPromises()

    await wrapper.find('input[name="surfaceKey"]').setValue('portal')
    await wrapper.find('input[name="surfaceDisplayName"]').setValue('Portal')
    await wrapper.find('select[name="surfaceAccessMode"]').setValue('Public')
    await wrapper.find('input[name="surfaceHost"]').setValue('portal.example.de')
    await wrapper.find('form.surfaces__form').trigger('submit')
    await flushPromises()

    expect(upsertSurfaceMock).toHaveBeenCalledTimes(1)
    const [ws, key, body] = upsertSurfaceMock.mock.calls[0]
    expect(ws).toBe('acme')
    expect(key).toBe('portal')
    expect(body.displayName).toBe('Portal')
    expect(body.accessMode).toBe('Public')
    expect(body.publicHost).toBe('portal.example.de')
    expect(body.surfaceType).toBe('spa') // default
    // A freshly created surface carries no template/theme.
    expect(body.themePluginId).toBeNull()
    expect(listSurfacesMock).toHaveBeenCalledTimes(2) // initial + reload
  })

  // ── Der Baum (ADR-019) ────────────────────────────────────────────────────

  it('zeigt den vollen Pfad eines Kindes, nicht sein gespeichertes Segment', async () => {
    // Ein Kind trägt `partner`, erreichbar ist es unter `/portal/partner`. Das Segment
    // anzuzeigen hieße, eine URL zu behaupten, die es nicht gibt.
    listSurfacesMock.mockResolvedValue([
      surface({ surfaceKey: 'portal', publicPathPrefix: '/portal', publicHost: 'kunde.example' }),
      surface({
        id: '2',
        surfaceKey: 'partner',
        parentSurfaceKey: 'portal',
        publicPathPrefix: 'partner',
        publicHost: null,
      }),
    ])
    const wrapper = mountSurfaces(true)
    await flushPromises()

    const locations = wrapper.findAll('.surfaces__location').map((el) => el.text())
    expect(locations).toContain('kunde.example/portal/partner')
  })

  it('rückt Kinder ein, statt die Hierarchie nur in der Reihenfolge zu verstecken', async () => {
    listSurfacesMock.mockResolvedValue([
      surface({ id: '2', surfaceKey: 'partner', parentSurfaceKey: 'portal' }),
      surface({ surfaceKey: 'portal' }),
    ])
    const wrapper = mountSurfaces(true)
    await flushPromises()

    const keys = wrapper.findAll('.surfaces__key')
    expect(keys[0].text()).toContain('portal')
    expect(keys[0].attributes('style')).toContain('--depth: 0')
    expect(keys[1].text()).toContain('partner')
    expect(keys[1].attributes('style')).toContain('--depth: 1')
  })

  it('legt eine Surface unter einer anderen an', async () => {
    listSurfacesMock.mockResolvedValue([surface({ surfaceKey: 'portal' })])
    const wrapper = mountSurfaces(true)
    await flushPromises()

    await wrapper.find('input[name="surfaceKey"]').setValue('partner')
    await wrapper.find('input[name="surfaceDisplayName"]').setValue('Partner')
    await wrapper.find('select[name="surfaceParent"]').setValue('portal')
    await wrapper.find('form.surfaces__form').trigger('submit')
    await flushPromises()

    expect(upsertSurfaceMock.mock.calls[0][2].parentSurfaceKey).toBe('portal')
  })

  it('bietet einen Knoten und seine Nachfahren nicht als Übergeordnetes an', async () => {
    // Der Server lehnt den Zyklus ohnehin ab; ihn gar nicht anzubieten ist der Unterschied
    // zwischen einer Fehlermeldung und einer Auswahl, die nur Mögliches enthält.
    listSurfacesMock.mockResolvedValue([
      // `portal` zuerst: Wurzeln sortieren nach position, und der Test bearbeitet die erste.
      surface({ surfaceKey: 'portal', position: 0 }),
      surface({ id: '2', surfaceKey: 'partner', parentSurfaceKey: 'portal' }),
      surface({ id: '3', surfaceKey: 'dialer', position: 1 }),
    ])
    const wrapper = mountSurfaces(true)
    await flushPromises()

    await wrapper.findAll('button.is-ghost').find((b) => b.text() === 'Bearbeiten')!.trigger('click')
    await flushPromises()

    const offered = wrapper
      .find('select[name="surfaceParent"]')
      .findAll('option')
      .map((option) => option.attributes('value'))
    expect(offered).not.toContain('portal')
    expect(offered).not.toContain('partner')
    expect(offered).toContain('dialer')
  })

  it('trennt eigene von geerbten Sichtbarkeits-Anforderungen', async () => {
    // Was von oben gilt, kann man hier nicht ändern — es sähe sonst aus wie eine Einstellung
    // dieses Knotens, und ein Speichern schriebe es hier fest.
    listSurfacesMock.mockResolvedValue([
      surface({ surfaceKey: 'portal', requiredClaims: 'kunde' }),
      surface({ id: '2', surfaceKey: 'partner', parentSurfaceKey: 'portal', requiredClaims: 'partner' }),
    ])
    const wrapper = mountSurfaces(true)
    await flushPromises()

    const text = wrapper.text()
    expect(text).toContain('partner')
    expect(text).toContain('kunde ↑')
  })

  it('speichert nur die eigenen Claims, nicht die geerbten', async () => {
    listSurfacesMock.mockResolvedValue([surface({ surfaceKey: 'portal', requiredClaims: 'kunde' })])
    const wrapper = mountSurfaces(true)
    await flushPromises()

    await wrapper.find('input[name="surfaceKey"]').setValue('partner')
    await wrapper.find('input[name="surfaceDisplayName"]').setValue('Partner')
    await wrapper.find('select[name="surfaceParent"]').setValue('portal')
    await wrapper.find('input[name="surfaceRequiredClaims"]').setValue(' partner , partner ')
    await wrapper.find('form.surfaces__form').trigger('submit')
    await flushPromises()

    // Entdoppelt und ohne Leerraum — und ohne „kunde", das vom Elternteil kommt.
    expect(upsertSurfaceMock.mock.calls[0][2].requiredClaims).toBe('partner')
  })

  it('does not submit without a key, name and path prefix', async () => {
    const wrapper = mountSurfaces(true)
    await flushPromises()

    // Only a display name, no key.
    await wrapper.find('input[name="surfaceDisplayName"]').setValue('Portal')
    await wrapper.find('input[name="surfaceKey"]').setValue('')
    await wrapper.find('form.surfaces__form').trigger('submit')
    await flushPromises()

    expect(upsertSurfaceMock).not.toHaveBeenCalled()
  })

  it('edits a surface and round-trips the carried theme fields untouched', async () => {
    const wrapper = mountSurfaces(true)
    await flushPromises()

    // Enter edit mode from the row action.
    await wrapper.findAll('button.is-ghost').find((b) => b.text() === 'Bearbeiten')!.trigger('click')
    // The key becomes read-only in edit mode.
    expect((wrapper.find('input[name="surfaceKey"]').element as HTMLInputElement).disabled).toBe(true)

    await wrapper.find('input[name="surfaceDisplayName"]').setValue('Renamed')
    await wrapper.find('form.surfaces__form').trigger('submit')
    await flushPromises()

    const [ws, key, body] = upsertSurfaceMock.mock.calls[0]
    expect(ws).toBe('acme')
    expect(key).toBe('default') // identity preserved from the edited surface
    expect(body.displayName).toBe('Renamed')
    // Theme was loaded from the surface and must survive the full-replace PUT.
    expect(body.themePluginId).toBe('acme.theme')
    expect(body.themeVersion).toBe('2.0.0')
  })

  it('aborts save when a before-save hook cancels', async () => {
    registerHook('workspaces.surface.before-save', (h) => h.cancel('gesperrt'))
    const wrapper = mountSurfaces(true)
    await flushPromises()

    await wrapper.find('input[name="surfaceKey"]').setValue('portal')
    await wrapper.find('input[name="surfaceDisplayName"]').setValue('Portal')
    await wrapper.find('form.surfaces__form').trigger('submit')
    await flushPromises()

    expect(upsertSurfaceMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('gesperrt')
  })

  it('removes a surface after confirmation and runs the after-remove hook', async () => {
    confirmMock.mockResolvedValue(true)
    const seen: unknown[] = []
    registerHook('workspaces.surface.after-remove', (h) => {
      seen.push(h.payload)
    })
    const wrapper = mountSurfaces(true)
    await flushPromises()

    await wrapper.find('.is-danger-ghost').trigger('click')
    await flushPromises()

    expect(removeSurfaceMock).toHaveBeenCalledWith('acme', 'default')
    expect(seen).toEqual([{ workspaceKey: 'acme', surfaceKey: 'default' }])
  })

  it('does not remove when the confirm dialog is dismissed', async () => {
    confirmMock.mockResolvedValue(false)
    const wrapper = mountSurfaces(true)
    await flushPromises()

    await wrapper.find('.is-danger-ghost').trigger('click')
    await flushPromises()

    expect(removeSurfaceMock).not.toHaveBeenCalled()
  })
})
