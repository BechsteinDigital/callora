import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import DashboardView from './DashboardView.vue'
import { useAuthStore } from '@/core/auth/authStore'
import type { AdminContext } from '@/core/auth/adminContext'

const { usersListMock, workspacesListMock, pluginsListMock, jobsListMock } = vi.hoisted(() => ({
  usersListMock: vi.fn(),
  workspacesListMock: vi.fn(),
  pluginsListMock: vi.fn(),
  jobsListMock: vi.fn(),
}))

vi.mock('@/modules/users/usersApi', () => ({ usersApi: { list: usersListMock } }))
vi.mock('@/modules/workspaces/workspacesApi', () => ({ workspacesApi: { list: workspacesListMock } }))
vi.mock('@/modules/plugins/pluginsApi', () => ({
  pluginsApi: { list: pluginsListMock },
  isPluginActive: (state: number) => state === 1,
}))
vi.mock('@/modules/jobs/jobsApi', () => ({ jobsApi: { list: jobsListMock } }))

function setContext(over: Partial<AdminContext>): void {
  useAuthStore().context.value = {
    userId: 'root',
    displayName: 'Root',
    email: null,
    roles: ['superadmin'],
    permissions: [],
    scope: 'platform',
    workspaceKey: null,
    isOperator: true,
    ...over,
  }
}

beforeEach(() => {
  useAuthStore().reset()
  usersListMock.mockReset().mockResolvedValue([{}, {}, {}]) // 3
  workspacesListMock.mockReset().mockResolvedValue([{}, {}]) // 2
  pluginsListMock.mockReset().mockResolvedValue([{ state: 1 }, { state: 1 }, { state: 2 }]) // 2 active
  jobsListMock.mockReset().mockResolvedValue([{}, {}, {}, {}]) // 4
})

describe('DashboardView', () => {
  it('shows all KPI cards with counts for a super admin', async () => {
    setContext({ permissions: ['*'] })
    const wrapper = mount(DashboardView)
    await flushPromises()

    expect(wrapper.findAll('.kpi')).toHaveLength(4)
    const text = wrapper.text()
    expect(text).toContain('Aktive Plugins')
    expect(text).toContain('3') // users
    expect(text).toContain('4') // jobs
  })

  it('shows only the metrics the caller may read and fetches nothing else', async () => {
    setContext({ permissions: ['user.read'], isOperator: false })
    const wrapper = mount(DashboardView)
    await flushPromises()

    const cards = wrapper.findAll('.kpi')
    expect(cards).toHaveLength(1)
    expect(cards[0].text()).toContain('Benutzer')
    // Metrics the caller cannot read are never fetched (would 403).
    expect(workspacesListMock).not.toHaveBeenCalled()
    expect(jobsListMock).not.toHaveBeenCalled()
  })

  it('renders an em dash when a metric fails to load', async () => {
    setContext({ permissions: ['user.read'] })
    usersListMock.mockRejectedValueOnce(new Error('boom'))
    const wrapper = mount(DashboardView)
    await flushPromises()

    expect(wrapper.find('.kpi-value').text()).toBe('—')
  })

  it('still renders the identity panel from the context', async () => {
    setContext({ permissions: [], roles: ['superadmin'] })
    const wrapper = mount(DashboardView)
    await flushPromises()

    expect(wrapper.findAll('.kpi')).toHaveLength(0) // no readable metrics
    const text = wrapper.text()
    expect(text).toContain('root')
    expect(text).toContain('platform')
    expect(text).toContain('superadmin')
    expect(text).toContain('ja')
  })

  it('renders the workspace binding for a workspace-scoped context', async () => {
    setContext({
      userId: 'alice',
      displayName: null,
      roles: ['admin'],
      permissions: ['workspace.read', 'flow.manage'],
      scope: 'workspace',
      workspaceKey: 'sales-de',
      isOperator: false,
    })
    const wrapper = mount(DashboardView)
    await flushPromises()

    expect(wrapper.text()).toContain('sales-de')
    expect(wrapper.findAll('.kpi')).toHaveLength(1) // only the Workspaces KPI is readable
    expect(wrapper.find('.kpi').text()).toContain('Workspaces')
  })
})
