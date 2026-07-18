import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import UserDetailView from './UserDetailView.vue'
import type { AdminContext } from '@/core/auth/adminContext'
import type { BackendUser } from './usersApi'
import { registerHook, resetHooks } from '@/core/extensions/hooks'

const {
  getMock,
  createMock,
  updateMock,
  assignRoleMock,
  listRolesMock,
  listRoleAssignmentsMock,
  pushMock,
  routeParams,
  contextRef,
} = vi.hoisted(() => ({
  getMock: vi.fn(),
  createMock: vi.fn(),
  updateMock: vi.fn(),
  assignRoleMock: vi.fn(),
  listRolesMock: vi.fn(),
  listRoleAssignmentsMock: vi.fn(),
  pushMock: vi.fn(),
  routeParams: { value: {} as Record<string, string> },
  contextRef: { value: null as AdminContext | null },
}))

vi.mock('./usersApi', () => ({
  usersApi: {
    get: getMock,
    create: createMock,
    update: updateMock,
    assignRole: assignRoleMock,
    listRoles: listRolesMock,
    listRoleAssignments: listRoleAssignmentsMock,
  },
}))
vi.mock('@/core/auth/authStore', () => ({ useAuthStore: () => ({ context: contextRef }) }))
vi.mock('vue-router', () => ({
  useRoute: () => ({ params: routeParams.value }),
  useRouter: () => ({ push: pushMock }),
  RouterLink: { name: 'RouterLink', props: ['to'], template: '<a><slot /></a>' },
}))

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

const existing: BackendUser = {
  externalId: 'op',
  email: 'op@x.io',
  displayName: 'Op',
  hasPassword: true,
  passwordHashAlgorithm: null,
  createdAtUtc: '',
  updatedAtUtc: '',
}

beforeEach(() => {
  for (const m of [getMock, createMock, updateMock, assignRoleMock, listRolesMock, listRoleAssignmentsMock, pushMock]) {
    m.mockReset()
  }
  listRolesMock.mockResolvedValue([{ role: 'superadmin', permissions: ['*'] }])
  resetHooks()
})

describe('UserDetailView', () => {
  it('creates a user and assigns the selected role', async () => {
    routeParams.value = {}
    contextRef.value = ctx(['*'])
    createMock.mockResolvedValue({ externalId: 'op' })
    assignRoleMock.mockResolvedValue(undefined)

    const wrapper = mount(UserDetailView)
    await flushPromises()
    await wrapper.find('input[name="externalId"]').setValue('op')
    await wrapper.find('input[name="password"]').setValue('secret')
    await wrapper.find('select[name="role"]').setValue('superadmin')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(createMock).toHaveBeenCalledWith({ externalId: 'op', email: null, displayName: null, password: 'secret' })
    expect(assignRoleMock).toHaveBeenCalledWith('op', 'superadmin')
    expect(pushMock).toHaveBeenCalledWith('/users')
  })

  it('sends a null password on edit when left empty and skips an unchanged role', async () => {
    routeParams.value = { userId: 'op' }
    contextRef.value = ctx(['*'])
    getMock.mockResolvedValue(existing)
    listRoleAssignmentsMock.mockResolvedValue({ op: 'superadmin' })
    updateMock.mockResolvedValue(existing)

    const wrapper = mount(UserDetailView)
    await flushPromises()
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(updateMock).toHaveBeenCalledWith('op', { email: 'op@x.io', displayName: 'Op', password: null })
    expect(assignRoleMock).not.toHaveBeenCalled()
  })

  it('hides the role picker without role permissions', async () => {
    routeParams.value = {}
    contextRef.value = ctx(['user.create'])

    const wrapper = mount(UserDetailView)
    await flushPromises()

    expect(wrapper.find('select[name="role"]').exists()).toBe(false)
    expect(listRolesMock).not.toHaveBeenCalled()
  })

  it('aborts save when a before-save hook cancels', async () => {
    routeParams.value = {}
    contextRef.value = ctx(['*'])
    registerHook('users.before-save', (h) => h.cancel('vom Plugin abgelehnt'))

    const wrapper = mount(UserDetailView)
    await flushPromises()
    await wrapper.find('input[name="externalId"]').setValue('op')
    await wrapper.find('input[name="password"]').setValue('secret')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(createMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('vom Plugin abgelehnt')
  })

  it('applies before-save hook mutations to the created user', async () => {
    routeParams.value = {}
    contextRef.value = ctx(['*'])
    createMock.mockResolvedValue({ externalId: 'op' })
    registerHook<{ email: string | null }>('users.before-save', (h) => {
      h.payload.email = 'hooked@x.io'
    })

    const wrapper = mount(UserDetailView)
    await flushPromises()
    await wrapper.find('input[name="externalId"]').setValue('op')
    await wrapper.find('input[name="password"]').setValue('secret')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(createMock).toHaveBeenCalledWith(expect.objectContaining({ email: 'hooked@x.io' }))
  })
})
