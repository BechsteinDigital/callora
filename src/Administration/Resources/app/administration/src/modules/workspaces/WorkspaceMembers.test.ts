import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import WorkspaceMembers from './WorkspaceMembers.vue'
import type { WorkspaceMember } from './workspacesApi'
import { registerHook, resetHooks } from '@/core/extensions/hooks'
import { resetServices } from '@/core/extensions/services'

const { listMembersMock, upsertMemberMock, removeMemberMock } = vi.hoisted(() => ({
  listMembersMock: vi.fn(),
  upsertMemberMock: vi.fn(),
  removeMemberMock: vi.fn(),
}))

vi.mock('./workspacesApi', () => ({
  workspacesApi: {
    listMembers: listMembersMock,
    upsertMember: upsertMemberMock,
    removeMember: removeMemberMock,
  },
}))

function member(over: Partial<WorkspaceMember>): WorkspaceMember {
  return {
    workspaceKey: 'acme',
    userId: 'alice',
    email: 'alice@x.io',
    displayName: 'Alice',
    role: 'admin',
    assignedAtUtc: '',
    ...over,
  }
}

function mountMembers(canManage: boolean) {
  return mount(WorkspaceMembers, { props: { workspaceKey: 'acme', canManage } })
}

beforeEach(() => {
  listMembersMock.mockReset().mockResolvedValue([member({ userId: 'alice', role: 'admin' })])
  upsertMemberMock.mockReset().mockResolvedValue(member({}))
  removeMemberMock.mockReset().mockResolvedValue(undefined)
  resetHooks()
  resetServices()
})

describe('WorkspaceMembers', () => {
  it('lists members for the workspace', async () => {
    const wrapper = mountMembers(true)
    await flushPromises()

    expect(listMembersMock).toHaveBeenCalledWith('acme')
    expect(wrapper.text()).toContain('Alice')
    expect(wrapper.text()).toContain('admin')
  })

  it('hides the add form and remove action without manage permission', async () => {
    const wrapper = mountMembers(false)
    await flushPromises()

    expect(wrapper.find('form.add').exists()).toBe(false)
    expect(wrapper.find('.link-danger').exists()).toBe(false)
  })

  it('assigns a member and reloads', async () => {
    const wrapper = mountMembers(true)
    await flushPromises()

    await wrapper.find('input[name="memberUserId"]').setValue('bob')
    await wrapper.find('input[name="memberRole"]').setValue('support')
    await wrapper.find('form.add').trigger('submit.prevent')
    await flushPromises()

    expect(upsertMemberMock).toHaveBeenCalledWith('acme', 'bob', 'support')
    expect(listMembersMock).toHaveBeenCalledTimes(2) // initial + reload
  })

  it('does not assign without both a user and a role', async () => {
    const wrapper = mountMembers(true)
    await flushPromises()

    await wrapper.find('input[name="memberUserId"]').setValue('bob')
    // role left empty
    await wrapper.find('form.add').trigger('submit.prevent')
    await flushPromises()

    expect(upsertMemberMock).not.toHaveBeenCalled()
  })

  it('lets a before-save hook mutate the role', async () => {
    registerHook<{ role: string }>('workspaces.member.before-save', (h) => {
      h.payload.role = 'viewer'
    })
    const wrapper = mountMembers(true)
    await flushPromises()

    await wrapper.find('input[name="memberUserId"]').setValue('bob')
    await wrapper.find('input[name="memberRole"]').setValue('support')
    await wrapper.find('form.add').trigger('submit.prevent')
    await flushPromises()

    expect(upsertMemberMock).toHaveBeenCalledWith('acme', 'bob', 'viewer')
  })

  it('aborts assign when a before-save hook cancels', async () => {
    registerHook('workspaces.member.before-save', (h) => h.cancel('gesperrt'))
    const wrapper = mountMembers(true)
    await flushPromises()

    await wrapper.find('input[name="memberUserId"]').setValue('bob')
    await wrapper.find('input[name="memberRole"]').setValue('support')
    await wrapper.find('form.add').trigger('submit.prevent')
    await flushPromises()

    expect(upsertMemberMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('gesperrt')
  })

  it('removes a member after confirmation and runs the after-remove hook', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const seen: unknown[] = []
    registerHook('workspaces.member.after-remove', (h) => {
      seen.push(h.payload)
    })
    const wrapper = mountMembers(true)
    await flushPromises()

    await wrapper.find('.link-danger').trigger('click')
    await flushPromises()

    expect(removeMemberMock).toHaveBeenCalledWith('acme', 'alice')
    expect(seen).toEqual([{ workspaceKey: 'acme', userId: 'alice' }])
  })

  it('aborts remove when a before-remove hook cancels', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    registerHook('workspaces.member.before-remove', (h) => h.cancel('geschützt'))
    const wrapper = mountMembers(true)
    await flushPromises()

    await wrapper.find('.link-danger').trigger('click')
    await flushPromises()

    expect(removeMemberMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('geschützt')
  })

  it('does not remove when the confirm dialog is dismissed', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false)
    const wrapper = mountMembers(true)
    await flushPromises()

    await wrapper.find('.link-danger').trigger('click')
    await flushPromises()

    expect(removeMemberMock).not.toHaveBeenCalled()
  })
})
