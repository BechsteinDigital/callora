import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import WorkspacesListView from './WorkspacesListView.vue'
import type { AdminContext } from '@/core/auth/adminContext'
import type { Workspace } from './workspacesApi'
import { registerHook, resetHooks } from '@/core/extensions/hooks'
import { resetServices } from '@/core/extensions/services'

const { listMock, removeMock, contextRef } = vi.hoisted(() => ({
  listMock: vi.fn(),
  removeMock: vi.fn(),
  contextRef: { value: null as AdminContext | null },
}))

vi.mock('./workspacesApi', () => ({ workspacesApi: { list: listMock, remove: removeMock } }))
vi.mock('@/core/auth/authStore', () => ({ useAuthStore: () => ({ context: contextRef }) }))

const RouterLinkStub = { name: 'RouterLink', props: ['to'], template: '<a><slot /></a>' }
const mountOptions = { global: { stubs: { RouterLink: RouterLinkStub } } }

function ctx(permissions: string[]): AdminContext {
  return {
    userId: 'u',
    displayName: null,
    email: null,
    roles: [],
    permissions,
    scope: null,
    workspaceKey: null,
    isOperator: false,
  }
}

function ws(over: Partial<Workspace>): Workspace {
  return {
    tenantKey: 't',
    workspaceKey: 'acme',
    displayName: 'Acme',
    workspaceType: 'standard',
    isActive: true,
    tenantIsActive: true,
    publicBaseUrl: null,
    publicHost: null,
    publicPathPrefix: '/',
    themePluginId: null,
    themeVersion: null,
    themeAssignedBy: null,
    themeAssignedAtUtc: null,
    createdAtUtc: '',
    updatedAtUtc: '',
    ...over,
  }
}

beforeEach(() => {
  listMock.mockReset().mockResolvedValue([ws({ workspaceKey: 'acme', displayName: 'Acme', isActive: true })])
  removeMock.mockReset().mockResolvedValue(undefined)
  resetHooks()
  resetServices()
})

describe('WorkspacesListView', () => {
  it('shows manage and delete actions with the right permissions', async () => {
    contextRef.value = ctx(['workspace.update', 'workspace.delete'])
    const wrapper = mount(WorkspacesListView, mountOptions)
    await flushPromises()

    const text = wrapper.text()
    expect(text).toContain('Acme')
    expect(text).toContain('Aktiv')
    expect(text).toContain('Neu anlegen')
    expect(wrapper.findAll('.link-danger')).toHaveLength(1)
  })

  it('hides manage/delete without the permissions', async () => {
    contextRef.value = ctx(['workspace.read'])
    const wrapper = mount(WorkspacesListView, mountOptions)
    await flushPromises()

    expect(wrapper.text()).not.toContain('Neu anlegen')
    expect(wrapper.findAll('.link-danger')).toHaveLength(0)
  })

  it('deletes after confirmation and reloads', async () => {
    contextRef.value = ctx(['workspace.delete'])
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const wrapper = mount(WorkspacesListView, mountOptions)
    await flushPromises()

    await wrapper.find('.link-danger').trigger('click')
    await flushPromises()

    expect(removeMock).toHaveBeenCalledWith('acme')
    expect(listMock).toHaveBeenCalledTimes(2) // initial + reload
  })

  it('does not delete when the confirm dialog is dismissed', async () => {
    contextRef.value = ctx(['workspace.delete'])
    vi.spyOn(window, 'confirm').mockReturnValue(false)
    const wrapper = mount(WorkspacesListView, mountOptions)
    await flushPromises()

    await wrapper.find('.link-danger').trigger('click')
    await flushPromises()

    expect(removeMock).not.toHaveBeenCalled()
  })

  it('runs the after-delete hook with the workspace key on success', async () => {
    contextRef.value = ctx(['workspace.delete'])
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const seen: unknown[] = []
    registerHook('workspaces.after-delete', (h) => {
      seen.push(h.payload)
    })
    const wrapper = mount(WorkspacesListView, mountOptions)
    await flushPromises()

    await wrapper.find('.link-danger').trigger('click')
    await flushPromises()

    expect(seen).toEqual([{ workspaceKey: 'acme' }])
  })

  it('aborts delete when a before-delete hook cancels', async () => {
    contextRef.value = ctx(['workspace.delete'])
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    registerHook('workspaces.before-delete', (h) => h.cancel('geschützt'))
    const wrapper = mount(WorkspacesListView, mountOptions)
    await flushPromises()

    await wrapper.find('.link-danger').trigger('click')
    await flushPromises()

    expect(removeMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('geschützt')
  })
})
