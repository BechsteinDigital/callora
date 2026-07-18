import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import RoleDetailView from './RoleDetailView.vue'
import type { Permission, Role } from './rolesApi'

const { listPermissionsMock, listMock, upsertMock, pushMock, routeParams } = vi.hoisted(() => ({
  listPermissionsMock: vi.fn(),
  listMock: vi.fn(),
  upsertMock: vi.fn(),
  pushMock: vi.fn(),
  routeParams: { value: {} as Record<string, string> },
}))

vi.mock('./rolesApi', () => ({
  rolesApi: { listPermissions: listPermissionsMock, list: listMock, upsert: upsertMock },
}))
vi.mock('vue-router', () => ({
  useRoute: () => ({ params: routeParams.value }),
  useRouter: () => ({ push: pushMock }),
  RouterLink: { name: 'RouterLink', props: ['to'], template: '<a><slot /></a>' },
}))

const permissions: Permission[] = [
  { permissionKey: 'user.read', function: 'user', action: 'read' },
  { permissionKey: 'user.create', function: 'user', action: 'create' },
]

beforeEach(() => {
  for (const m of [listPermissionsMock, listMock, upsertMock, pushMock]) {
    m.mockReset()
  }
  listPermissionsMock.mockResolvedValue(permissions)
})

describe('RoleDetailView', () => {
  it('creates a role with the checked permissions', async () => {
    routeParams.value = {}
    upsertMock.mockResolvedValue(undefined)

    const wrapper = mount(RoleDetailView)
    await flushPromises()
    await wrapper.find('input[name="role"]').setValue('support')
    await wrapper.findAll('input[type="checkbox"]')[0].setValue(true) // user.read
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(upsertMock).toHaveBeenCalledWith('support', ['user.read'])
    expect(pushMock).toHaveBeenCalledWith('/roles')
  })

  it('prefills the current permissions in edit mode, dropping the wildcard', async () => {
    routeParams.value = { role: 'support' }
    const current: Role = { role: 'support', permissions: ['user.read', '*'] }
    listMock.mockResolvedValue([current])

    const wrapper = mount(RoleDetailView)
    await flushPromises()

    const checkboxes = wrapper.findAll('input[type="checkbox"]')
    expect((checkboxes[0].element as HTMLInputElement).checked).toBe(true) // user.read
    expect((checkboxes[1].element as HTMLInputElement).checked).toBe(false) // user.create
  })
})
