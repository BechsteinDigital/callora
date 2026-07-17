import { describe, it, expect, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import DashboardView from './DashboardView.vue'
import { useAuthStore } from '@/core/auth/authStore'

beforeEach(() => useAuthStore().reset())

describe('DashboardView', () => {
  it('renders identity, scope and operator flag from the context', () => {
    useAuthStore().context.value = {
      userId: 'root',
      displayName: 'Root',
      email: null,
      roles: ['superadmin'],
      permissions: [],
      scope: 'platform',
      workspaceKey: null,
      isOperator: true,
    }

    const wrapper = mount(DashboardView)

    expect(wrapper.text()).toContain('root')
    expect(wrapper.text()).toContain('platform')
    expect(wrapper.text()).toContain('superadmin')
    expect(wrapper.text()).toContain('ja')
  })

  it('renders the workspace binding for a workspace-scoped context', () => {
    useAuthStore().context.value = {
      userId: 'alice',
      displayName: null,
      email: null,
      roles: ['admin'],
      permissions: ['workspace.read', 'flow.manage'],
      scope: 'workspace',
      workspaceKey: 'sales-de',
      isOperator: false,
    }

    const wrapper = mount(DashboardView)

    expect(wrapper.text()).toContain('sales-de')
    expect(wrapper.text()).toContain('2')
  })
})
