import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import WorkspaceSwitcher from './WorkspaceSwitcher.vue'
import { useWorkspaceContext, resetWorkspaceContext } from './workspaceContext'
import type { AdminContext } from '@/core/auth/adminContext'

const { listMock, contextRef } = vi.hoisted(() => ({
  listMock: vi.fn(),
  contextRef: { value: null as AdminContext | null },
}))

vi.mock('@/modules/workspaces/workspacesApi', () => ({ workspacesApi: { list: listMock } }))
vi.mock('@/core/auth/authStore', () => ({ useAuthStore: () => ({ context: contextRef }) }))

function ctx(workspaceKey: string | null): AdminContext {
  return {
    userId: 'u',
    displayName: null,
    email: null,
    roles: [],
    permissions: ['*'],
    scope: null,
    workspaceKey,
    tenantKey: null,
    isOperator: workspaceKey === null,
  }
}

beforeEach(() => {
  listMock.mockReset().mockResolvedValue([
    { workspaceKey: 'wsA', displayName: 'A' },
    { workspaceKey: 'wsB', displayName: 'B' },
  ])
  resetWorkspaceContext()
})

describe('WorkspaceSwitcher', () => {
  it('renders a dropdown of the operator workspaces', async () => {
    contextRef.value = ctx(null)
    const wrapper = mount(WorkspaceSwitcher)
    await flushPromises()

    const select = wrapper.find('select[name="active-workspace"]')
    expect(select.exists()).toBe(true)
    expect(select.findAll('option')).toHaveLength(2)
    expect((select.element as HTMLSelectElement).value).toBe('wsA')
  })

  it('renders nothing for a workspace-bound admin', async () => {
    contextRef.value = ctx('ws1')
    const wrapper = mount(WorkspaceSwitcher)
    await flushPromises()

    expect(wrapper.find('select[name="active-workspace"]').exists()).toBe(false)
  })

  it('switches the shared active workspace on change', async () => {
    contextRef.value = ctx(null)
    const wrapper = mount(WorkspaceSwitcher)
    await flushPromises()

    await wrapper.find('select[name="active-workspace"]').setValue('wsB')
    await flushPromises()

    expect(useWorkspaceContext().activeWorkspace.value).toBe('wsB')
  })
})
