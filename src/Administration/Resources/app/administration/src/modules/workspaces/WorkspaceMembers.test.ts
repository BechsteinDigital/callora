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

// The confirm dialog is a promise-based store now, not window.confirm — mock it so
// each test can decide what the operator answers.
const { confirmMock } = vi.hoisted(() => ({ confirmMock: vi.fn() }))
vi.mock('@/core/feedback/confirm', () => ({ confirm: confirmMock }))

beforeEach(() => {
  confirmMock.mockReset().mockResolvedValue(true)
})

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

function page(items: WorkspaceMember[], nextCursor: string | null = null, total = items.length) {
  return { items, total, nextCursor }
}

beforeEach(() => {
  listMembersMock.mockReset().mockResolvedValue(page([member({ userId: 'alice', role: 'admin' })]))
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

    expect(wrapper.find('form.members__form').exists()).toBe(false)
    expect(wrapper.find('.is-danger-ghost').exists()).toBe(false)
  })

  it('loads and appends the next page via the cursor', async () => {
    listMembersMock
      .mockResolvedValueOnce(page([member({ userId: 'alice' })], 'cursor-1', 2))
      .mockResolvedValueOnce(page([member({ userId: 'bob', displayName: 'Bob' })], null, 2))
    const wrapper = mountMembers(false)
    await flushPromises()

    // First page shows the "Mehr laden" affordance while a cursor remains.
    const moreButton = wrapper.findAll('button').find((b) => b.text().includes('Mehr laden'))
    expect(moreButton).toBeDefined()
    expect(wrapper.text()).not.toContain('Bob')

    await moreButton!.trigger('click')
    await flushPromises()

    expect(listMembersMock).toHaveBeenLastCalledWith('acme', 'cursor-1')
    expect(wrapper.text()).toContain('alice')
    expect(wrapper.text()).toContain('Bob')
    // Cursor exhausted → no more button.
    expect(wrapper.findAll('button').find((b) => b.text().includes('Mehr laden'))).toBeUndefined()
  })

  it('resets to the first page after a mutation (no accumulated leak)', async () => {
    confirmMock.mockResolvedValue(true)
    listMembersMock
      .mockResolvedValueOnce(page([member({ userId: 'alice' })], 'cursor-1', 2))
      .mockResolvedValueOnce(page([member({ userId: 'bob', displayName: 'Bob' })], null, 2))
      .mockResolvedValueOnce(page([member({ userId: 'alice' })], 'cursor-1', 2)) // reload after remove
    const wrapper = mountMembers(true)
    await flushPromises()

    await wrapper.findAll('button').find((b) => b.text().includes('Mehr laden'))!.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Bob') // page 2 accumulated

    await wrapper.find('.is-danger-ghost').trigger('click') // remove → reload from page 1
    await flushPromises()

    expect(wrapper.text()).not.toContain('Bob') // accumulated page dropped on reload
    expect(wrapper.text()).toContain('alice')
  })

  it('ignores a second load-more while one is in flight', async () => {
    let resolveSecond!: (value: unknown) => void
    listMembersMock
      .mockResolvedValueOnce(page([member({ userId: 'alice' })], 'cursor-1', 2))
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            resolveSecond = resolve
          }),
      )
    const wrapper = mountMembers(false)
    await flushPromises()

    const button = wrapper.findAll('button').find((b) => b.text().includes('Mehr laden'))!
    await button.trigger('click') // loadMore in flight
    await button.trigger('click') // guarded by loadingMore

    expect(listMembersMock).toHaveBeenCalledTimes(2) // initial + exactly one loadMore
    resolveSecond(page([member({ userId: 'bob' })], null, 2))
    await flushPromises()
  })

  it('assigns a member and reloads', async () => {
    const wrapper = mountMembers(true)
    await flushPromises()

    await wrapper.find('input[name="memberUserId"]').setValue('bob')
    await wrapper.find('input[name="memberRole"]').setValue('support')
    await wrapper.find('form.members__form').trigger('submit.prevent')
    await flushPromises()

    expect(upsertMemberMock).toHaveBeenCalledWith('acme', 'bob', 'support')
    expect(listMembersMock).toHaveBeenCalledTimes(2) // initial + reload
  })

  it('does not assign without both a user and a role', async () => {
    const wrapper = mountMembers(true)
    await flushPromises()

    await wrapper.find('input[name="memberUserId"]').setValue('bob')
    // role left empty
    await wrapper.find('form.members__form').trigger('submit.prevent')
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
    await wrapper.find('form.members__form').trigger('submit.prevent')
    await flushPromises()

    expect(upsertMemberMock).toHaveBeenCalledWith('acme', 'bob', 'viewer')
  })

  it('aborts assign when a before-save hook cancels', async () => {
    registerHook('workspaces.member.before-save', (h) => h.cancel('gesperrt'))
    const wrapper = mountMembers(true)
    await flushPromises()

    await wrapper.find('input[name="memberUserId"]').setValue('bob')
    await wrapper.find('input[name="memberRole"]').setValue('support')
    await wrapper.find('form.members__form').trigger('submit.prevent')
    await flushPromises()

    expect(upsertMemberMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('gesperrt')
  })

  it('removes a member after confirmation and runs the after-remove hook', async () => {
    confirmMock.mockResolvedValue(true)
    const seen: unknown[] = []
    registerHook('workspaces.member.after-remove', (h) => {
      seen.push(h.payload)
    })
    const wrapper = mountMembers(true)
    await flushPromises()

    await wrapper.find('.is-danger-ghost').trigger('click')
    await flushPromises()

    expect(removeMemberMock).toHaveBeenCalledWith('acme', 'alice')
    expect(seen).toEqual([{ workspaceKey: 'acme', userId: 'alice' }])
  })

  it('aborts remove when a before-remove hook cancels', async () => {
    confirmMock.mockResolvedValue(true)
    registerHook('workspaces.member.before-remove', (h) => h.cancel('geschützt'))
    const wrapper = mountMembers(true)
    await flushPromises()

    await wrapper.find('.is-danger-ghost').trigger('click')
    await flushPromises()

    expect(removeMemberMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('geschützt')
  })

  it('does not remove when the confirm dialog is dismissed', async () => {
    confirmMock.mockResolvedValue(false)
    const wrapper = mountMembers(true)
    await flushPromises()

    await wrapper.find('.is-danger-ghost').trigger('click')
    await flushPromises()

    expect(removeMemberMock).not.toHaveBeenCalled()
  })
})
