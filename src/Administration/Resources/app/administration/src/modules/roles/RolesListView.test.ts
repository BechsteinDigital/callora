import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import RolesListView from './RolesListView.vue'
import type { AdminContext } from '@/core/auth/adminContext'
import type { Role } from './rolesApi'

const { listMock, contextRef } = vi.hoisted(() => ({
  listMock: vi.fn(),
  contextRef: { value: null as AdminContext | null },
}))

vi.mock('./rolesApi', () => ({
  SYSTEM_ROLE: 'superadmin',
  rolesApi: { list: listMock, remove: vi.fn() },
}))
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

const sampleRoles: Role[] = [
  { role: 'superadmin', permissions: ['*'] },
  { role: 'support', permissions: ['user.read'] },
]

beforeEach(() => {
  listMock.mockReset().mockResolvedValue(sampleRoles)
})

describe('RolesListView', () => {
  it('shows manage actions for custom roles but not the system role', async () => {
    contextRef.value = ctx(['*'])
    const wrapper = mount(RolesListView, mountOptions)
    await flushPromises()

    const text = wrapper.text()
    expect(text).toContain('Neu anlegen')
    expect(text).toContain('System') // badge on superadmin
    expect(text).toContain('alle (*)')
    // superadmin is read-only, support is editable → exactly one delete action.
    expect(wrapper.findAll('.link-danger')).toHaveLength(1)
  })

  it('hides manage actions without role.update', async () => {
    contextRef.value = ctx(['role.read'])
    const wrapper = mount(RolesListView, mountOptions)
    await flushPromises()

    expect(wrapper.text()).not.toContain('Neu anlegen')
    expect(wrapper.findAll('.link-danger')).toHaveLength(0)
  })
})
