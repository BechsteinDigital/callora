import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import WorkspaceDetailView from './WorkspaceDetailView.vue'
import type { Workspace } from './workspacesApi'
import { registerHook, resetHooks } from '@/core/extensions/hooks'
import { resetServices } from '@/core/extensions/services'

const { getMock, upsertMock, pushMock, routeParams } = vi.hoisted(() => ({
  getMock: vi.fn(),
  upsertMock: vi.fn(),
  pushMock: vi.fn(),
  routeParams: { value: {} as Record<string, string> },
}))

vi.mock('./workspacesApi', () => ({ workspacesApi: { get: getMock, upsert: upsertMock } }))
// The members and surfaces sub-resources have their own tests; stub them here so
// the detail-view tests stay focused on the workspace fields.
vi.mock('./WorkspaceMembers.vue', () => ({ default: { name: 'WorkspaceMembers', template: '<div />' } }))
vi.mock('./WorkspaceSurfaces.vue', () => ({ default: { name: 'WorkspaceSurfaces', template: '<div />' } }))
vi.mock('./WorkspacePlugins.vue', () => ({ default: { name: 'WorkspacePlugins', template: '<div />' } }))
vi.mock('@/core/auth/authStore', () => ({ useAuthStore: () => ({ context: { value: null } }) }))
vi.mock('vue-router', () => ({
  useRoute: () => ({ params: routeParams.value }),
  useRouter: () => ({ push: pushMock }),
  RouterLink: { name: 'RouterLink', props: ['to'], template: '<a><slot /></a>' },
}))

const existing: Workspace = {
  tenantKey: 't',
  workspaceKey: 'acme',
  displayName: 'Acme',
  workspaceType: 'standard',
  isActive: true,
  tenantIsActive: true,
  publicBaseUrl: 'https://acme.test',
  publicHost: 'acme.test',
  publicPathPrefix: '/',
  themePluginId: null,
  themeVersion: null,
  themeAssignedBy: null,
  themeAssignedAtUtc: null,
  createdAtUtc: '',
  updatedAtUtc: '',
}

beforeEach(() => {
  for (const m of [getMock, upsertMock, pushMock]) {
    m.mockReset()
  }
  resetHooks()
  resetServices()
})

describe('WorkspaceDetailView', () => {
  it('creates a workspace via the keyed upsert', async () => {
    routeParams.value = {}
    upsertMock.mockResolvedValue(existing)

    const wrapper = mount(WorkspaceDetailView)
    await flushPromises()
    await wrapper.find('input[name="workspaceKey"]').setValue('acme')
    await wrapper.find('input[name="displayName"]').setValue('Acme')
    await wrapper.find('input[name="workspaceType"]').setValue('standard')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(upsertMock).toHaveBeenCalledWith('acme', {
      displayName: 'Acme',
      workspaceType: 'standard',
      isActive: true,
      publicBaseUrl: null,
    })
    expect(pushMock).toHaveBeenCalledWith('/workspaces')
  })

  it('prefills on edit and keeps the key read-only', async () => {
    routeParams.value = { workspaceKey: 'acme' }
    getMock.mockResolvedValue(existing)
    upsertMock.mockResolvedValue(existing)

    const wrapper = mount(WorkspaceDetailView)
    await flushPromises()

    const keyInput = wrapper.find('input[name="workspaceKey"]').element as HTMLInputElement
    expect(keyInput.value).toBe('acme')
    expect(keyInput.disabled).toBe(true)
    expect(wrapper.text()).toContain('acme.test') // read-only public host in meta block

    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()
    expect(upsertMock).toHaveBeenCalledWith('acme', {
      displayName: 'Acme',
      workspaceType: 'standard',
      isActive: true,
      publicBaseUrl: 'https://acme.test',
    })
  })

  it('does not submit without the required fields', async () => {
    routeParams.value = {}
    const wrapper = mount(WorkspaceDetailView)
    await flushPromises()

    await wrapper.find('input[name="workspaceKey"]').setValue('acme')
    // displayName and workspaceType left empty
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(upsertMock).not.toHaveBeenCalled()
  })

  it('aborts save when a before-save hook cancels', async () => {
    routeParams.value = {}
    registerHook('workspaces.before-save', (h) => h.cancel('vom Plugin abgelehnt'))

    const wrapper = mount(WorkspaceDetailView)
    await flushPromises()
    await wrapper.find('input[name="workspaceKey"]').setValue('acme')
    await wrapper.find('input[name="displayName"]').setValue('Acme')
    await wrapper.find('input[name="workspaceType"]').setValue('standard')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(upsertMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('vom Plugin abgelehnt')
  })

  it('runs the after-save hook with the workspace key on success', async () => {
    routeParams.value = {}
    upsertMock.mockResolvedValue(existing)
    const seen: unknown[] = []
    registerHook('workspaces.after-save', (h) => {
      seen.push(h.payload)
    })

    const wrapper = mount(WorkspaceDetailView)
    await flushPromises()
    await wrapper.find('input[name="workspaceKey"]').setValue('acme')
    await wrapper.find('input[name="displayName"]').setValue('Acme')
    await wrapper.find('input[name="workspaceType"]').setValue('standard')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(seen).toEqual([{ workspaceKey: 'acme' }])
  })

  it('applies before-save hook mutations to the upserted workspace', async () => {
    routeParams.value = {}
    upsertMock.mockResolvedValue(existing)
    registerHook<{ isActive: boolean }>('workspaces.before-save', (h) => {
      h.payload.isActive = false
    })

    const wrapper = mount(WorkspaceDetailView)
    await flushPromises()
    await wrapper.find('input[name="workspaceKey"]').setValue('acme')
    await wrapper.find('input[name="displayName"]').setValue('Acme')
    await wrapper.find('input[name="workspaceType"]').setValue('standard')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(upsertMock).toHaveBeenCalledWith('acme', expect.objectContaining({ isActive: false }))
  })
})
