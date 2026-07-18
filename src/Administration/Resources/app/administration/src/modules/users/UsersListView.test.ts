import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import UsersListView from './UsersListView.vue'
import type { AdminContext } from '@/core/auth/adminContext'
import type { BackendUser } from './usersApi'

const { listMock, roleAssignmentsMock, contextRef } = vi.hoisted(() => ({
  listMock: vi.fn(),
  roleAssignmentsMock: vi.fn(),
  contextRef: { value: null as AdminContext | null },
}))

vi.mock('./usersApi', () => ({
  usersApi: { list: listMock, listRoleAssignments: roleAssignmentsMock },
}))
vi.mock('@/core/auth/authStore', () => ({
  useAuthStore: () => ({ context: contextRef }),
}))

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

const sampleUser: BackendUser = {
  externalId: 'admin',
  email: null,
  displayName: null,
  hasPassword: true,
  passwordHashAlgorithm: null,
  createdAtUtc: '',
  updatedAtUtc: '',
}

beforeEach(() => {
  listMock.mockReset().mockResolvedValue([sampleUser])
  roleAssignmentsMock.mockReset().mockResolvedValue({ admin: 'superadmin' })
})

describe('UsersListView', () => {
  it('shows create/delete and the role column for a super admin (* wildcard)', async () => {
    contextRef.value = ctx(['*'])
    const wrapper = mount(UsersListView, mountOptions)
    await flushPromises()

    expect(wrapper.text()).toContain('Neu anlegen')
    expect(wrapper.text()).toContain('Löschen')
    expect(wrapper.text()).toContain('Rolle')
    expect(wrapper.text()).toContain('superadmin')
  })

  it('hides create/delete and the role column for a read-only caller', async () => {
    contextRef.value = ctx(['user.read'])
    const wrapper = mount(UsersListView, mountOptions)
    await flushPromises()

    expect(wrapper.text()).not.toContain('Neu anlegen')
    expect(wrapper.text()).not.toContain('Löschen')
    // Without role.read the role assignments must never be fetched (would 403).
    expect(roleAssignmentsMock).not.toHaveBeenCalled()
  })
})
