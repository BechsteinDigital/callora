import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import ThemesView from './ThemesView.vue'
import type { AdminContext } from '@/core/auth/adminContext'
import type { ThemeAssignment, ThemeDefinition } from './themesApi'
import { registerHook, resetHooks } from '@/core/extensions/hooks'
import { resetServices } from '@/core/extensions/services'
import { resetWorkspaceContext } from '@/core/workspace/workspaceContext'

const { listDefsMock, getAssignmentMock, assignMock, clearMock, contextRef } = vi.hoisted(() => ({
  listDefsMock: vi.fn(),
  getAssignmentMock: vi.fn(),
  assignMock: vi.fn(),
  clearMock: vi.fn(),
  contextRef: { value: null as AdminContext | null },
}))

vi.mock('./themesApi', () => ({
  themesApi: {
    listDefinitions: listDefsMock,
    getAssignment: getAssignmentMock,
    assign: assignMock,
    clearAssignment: clearMock,
  },
}))
vi.mock('@/modules/workspaces/workspacesApi', () => ({ workspacesApi: { list: vi.fn().mockResolvedValue([]) } }))
vi.mock('@/core/auth/authStore', () => ({ useAuthStore: () => ({ context: contextRef }) }))
// The settings editor has its own test; stub it so the assignment tests stay focused.
vi.mock('./ThemeSettings.vue', () => ({ default: { name: 'ThemeSettings', template: '<div />' } }))

function ctx(permissions: string[]): AdminContext {
  return {
    userId: 'u',
    displayName: null,
    email: null,
    roles: [],
    permissions,
    scope: null,
    workspaceKey: 'workspace-a', // fixed workspace → no picker
    isOperator: false,
  }
}

function def(over: Partial<ThemeDefinition>): ThemeDefinition {
  return {
    templateKey: 'website',
    surface: 'workspace',
    pluginId: 'customer.theme',
    version: '1.0.0',
    displayName: 'Customer Theme',
    templatePath: '',
    parentTemplateKey: null,
    scope: 'workspace',
    isActive: true,
    priority: 100,
    createdAtUtc: '',
    updatedAtUtc: '',
    ...over,
  }
}

function assignment(over: Partial<ThemeAssignment>): ThemeAssignment {
  return {
    workspaceKey: 'workspace-a',
    themePluginId: 'customer.theme',
    themeVersion: '1.0.0',
    assignedBy: 'root',
    assignedAtUtc: '',
    ...over,
  }
}

beforeEach(() => {
  listDefsMock.mockReset().mockResolvedValue([def({})])
  getAssignmentMock.mockReset().mockResolvedValue(null)
  assignMock.mockReset().mockResolvedValue(assignment({}))
  clearMock.mockReset().mockResolvedValue(undefined)
  resetHooks()
  resetServices()
  resetWorkspaceContext()
})

describe('ThemesView', () => {
  it('shows the current assignment when a theme is assigned', async () => {
    contextRef.value = ctx(['*'])
    getAssignmentMock.mockResolvedValueOnce(assignment({ themePluginId: 'acme.theme', themeVersion: '2.0.0' }))
    const wrapper = mount(ThemesView)
    await flushPromises()

    expect(wrapper.text()).toContain('acme.theme@2.0.0')
  })

  it('shows the empty state when no theme is assigned', async () => {
    contextRef.value = ctx(['*'])
    const wrapper = mount(ThemesView)
    await flushPromises()

    expect(wrapper.text()).toContain('Kein Theme zugewiesen')
  })

  it('hides the assign form and clear action without extension.update', async () => {
    contextRef.value = ctx(['extension.read'])
    getAssignmentMock.mockResolvedValueOnce(assignment({}))
    const wrapper = mount(ThemesView)
    await flushPromises()

    expect(wrapper.find('form.assign').exists()).toBe(false)
    expect(wrapper.find('.link-danger').exists()).toBe(false)
  })

  it('assigns the selected theme definition', async () => {
    contextRef.value = ctx(['*'])
    listDefsMock.mockResolvedValueOnce([
      def({ pluginId: 'a.theme', version: '1.0.0' }),
      def({ templateKey: 'portal', pluginId: 'b.theme', version: '3.0.0' }),
    ])
    const wrapper = mount(ThemesView)
    await flushPromises()

    await wrapper.find('select[name="themeDefinition"]').setValue('1')
    await wrapper.find('form.assign').trigger('submit')
    await flushPromises()

    expect(assignMock).toHaveBeenCalledWith('workspace-a', 'b.theme', '3.0.0')
  })

  it('clears the assignment after confirmation', async () => {
    contextRef.value = ctx(['*'])
    getAssignmentMock.mockResolvedValueOnce(assignment({}))
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const wrapper = mount(ThemesView)
    await flushPromises()

    await wrapper.find('.link-danger').trigger('click')
    await flushPromises()

    expect(clearMock).toHaveBeenCalledWith('workspace-a')
  })

  it('aborts assign when a before-assign hook cancels', async () => {
    contextRef.value = ctx(['*'])
    registerHook('themes.before-assign', (h) => h.cancel('gesperrt'))
    const wrapper = mount(ThemesView)
    await flushPromises()

    await wrapper.find('form.assign').trigger('submit')
    await flushPromises()

    expect(assignMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('gesperrt')
  })
})
