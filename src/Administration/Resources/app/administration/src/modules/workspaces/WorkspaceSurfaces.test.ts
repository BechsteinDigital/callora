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
    locale: null,
    templatePluginId: null,
    templateVersion: null,
    themePluginId: 'acme.theme',
    themeVersion: '2.0.0',
    isActive: true,
    createdAtUtc: '',
    updatedAtUtc: '',
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
