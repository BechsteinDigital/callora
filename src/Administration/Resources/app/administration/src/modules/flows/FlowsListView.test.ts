import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import FlowsListView from './FlowsListView.vue'
import type { AdminContext } from '@/core/auth/adminContext'
import type { Flow, FlowsPage } from './flowsApi'
import { registerHook, resetHooks } from '@/core/extensions/hooks'
import { resetServices } from '@/core/extensions/services'
import { resetWorkspaceContext } from '@/core/workspace/workspaceContext'

const { listMock, createMock, updateMock, removeMock, contextRef } = vi.hoisted(() => ({
  listMock: vi.fn(),
  createMock: vi.fn(),
  updateMock: vi.fn(),
  removeMock: vi.fn(),
  contextRef: { value: null as AdminContext | null },
}))

vi.mock('./flowsApi', () => ({
  FLOWS_PAGE_SIZE: 50,
  flowsApi: { list: listMock, create: createMock, update: updateMock, remove: removeMock },
}))
// A fixed-workspace context never calls this, but the import must resolve.
vi.mock('@/modules/workspaces/workspacesApi', () => ({ workspacesApi: { list: vi.fn().mockResolvedValue([]) } }))
vi.mock('@/core/auth/authStore', () => ({ useAuthStore: () => ({ context: contextRef }) }))

// The confirm dialog is a promise-based store now, not window.confirm — mock it so
// each test can decide what the operator answers.
const { confirmMock } = vi.hoisted(() => ({ confirmMock: vi.fn() }))
vi.mock('@/core/feedback/confirm', () => ({ confirm: confirmMock }))

beforeEach(() => {
  confirmMock.mockReset().mockResolvedValue(true)
})

function ctx(permissions: string[]): AdminContext {
  return {
    userId: 'u',
    displayName: null,
    email: null,
    roles: [],
    permissions,
    scope: null,
    workspaceKey: 'workspace-a', // fixed workspace → no picker, no workspacesApi.list
    isOperator: false,
  }
}

function flow(over: Partial<Flow>): Flow {
  return {
    id: 'f1',
    workspaceKey: 'workspace-a',
    name: 'Route to queue',
    triggerEvent: 'call.received',
    conditionsJson: null,
    actionsJson: '[]',
    isActive: true,
    priority: 100,
    createdAtUtc: '',
    ...over,
  }
}

function page(items: Flow[], nextCursor: string | null = null, total = items.length): FlowsPage {
  return { items, total, nextCursor }
}

beforeEach(() => {
  listMock.mockReset().mockResolvedValue(page([flow({})]))
  createMock.mockReset().mockResolvedValue(flow({}))
  updateMock.mockReset().mockResolvedValue(flow({}))
  removeMock.mockReset().mockResolvedValue(undefined)
  resetHooks()
  resetServices()
  resetWorkspaceContext()
})

describe('FlowsListView', () => {
  it('lists flows for the fixed workspace', async () => {
    contextRef.value = ctx(['*'])
    const wrapper = mount(FlowsListView)
    await flushPromises()

    expect(listMock).toHaveBeenCalledWith('workspace-a')
    const text = wrapper.text()
    expect(text).toContain('Route to queue')
    expect(text).toContain('call.received')
    expect(text).toContain('Aktiv')
  })

  it('hides the form and row actions without flow.manage', async () => {
    contextRef.value = ctx(['flow.read'])
    const wrapper = mount(FlowsListView)
    await flushPromises()

    expect(wrapper.find('form.flows__form').exists()).toBe(false)
    expect(wrapper.find('.is-danger-ghost').exists()).toBe(false)
  })

  it('creates a flow with parsed JSON (empty conditions → null, default actions → [])', async () => {
    contextRef.value = ctx(['*'])
    const wrapper = mount(FlowsListView)
    await flushPromises()

    await wrapper.find('input[name="flowName"]').setValue('New flow')
    await wrapper.find('input[name="flowTrigger"]').setValue('call.ended')
    await wrapper.find('form.flows__form').trigger('submit')
    await flushPromises()

    expect(createMock).toHaveBeenCalledTimes(1)
    const [ws, input] = createMock.mock.calls[0]
    expect(ws).toBe('workspace-a')
    expect(input).toEqual({
      name: 'New flow',
      triggerEvent: 'call.ended',
      conditions: null,
      actions: [],
      isActive: true,
      priority: 100,
    })
    expect(listMock).toHaveBeenCalledTimes(2) // initial + reload
  })

  it('rejects invalid JSON in a field before calling the API', async () => {
    contextRef.value = ctx(['*'])
    const wrapper = mount(FlowsListView)
    await flushPromises()

    await wrapper.find('input[name="flowName"]').setValue('New flow')
    await wrapper.find('input[name="flowTrigger"]').setValue('call.ended')
    await wrapper.find('textarea[name="flowConditions"]').setValue('{ not json')
    await wrapper.find('form.flows__form').trigger('submit')
    await flushPromises()

    expect(createMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Bedingungen enthält kein gültiges JSON.')
  })

  it('edits an existing flow via the update route', async () => {
    contextRef.value = ctx(['*'])
    listMock.mockResolvedValueOnce(page([flow({ id: 'f1', name: 'Route to queue' })]))
    const wrapper = mount(FlowsListView)
    await flushPromises()

    await wrapper.findAll('button.is-ghost').find((b) => b.text() === 'Bearbeiten')!.trigger('click')
    expect((wrapper.find('input[name="flowName"]').element as HTMLInputElement).value).toBe('Route to queue')

    await wrapper.find('input[name="flowName"]').setValue('Renamed')
    await wrapper.find('form.flows__form').trigger('submit')
    await flushPromises()

    const [ws, id, input] = updateMock.mock.calls[0]
    expect(ws).toBe('workspace-a')
    expect(id).toBe('f1')
    expect(input.name).toBe('Renamed')
    expect(createMock).not.toHaveBeenCalled()
  })

  it('deletes a flow only after confirmation', async () => {
    contextRef.value = ctx(['*'])
    confirmMock.mockResolvedValue(false)
    const wrapper = mount(FlowsListView)
    await flushPromises()

    await wrapper.find('.is-danger-ghost').trigger('click')
    await flushPromises()
    expect(removeMock).not.toHaveBeenCalled()

    confirmMock.mockResolvedValue(true)
    await wrapper.find('.is-danger-ghost').trigger('click')
    await flushPromises()
    expect(removeMock).toHaveBeenCalledWith('workspace-a', 'f1')

  })

  it('aborts save when a before-save hook cancels', async () => {
    contextRef.value = ctx(['*'])
    registerHook('flows.before-save', (h) => h.cancel('gesperrt'))
    const wrapper = mount(FlowsListView)
    await flushPromises()

    await wrapper.find('input[name="flowName"]').setValue('New flow')
    await wrapper.find('input[name="flowTrigger"]').setValue('call.ended')
    await wrapper.find('form.flows__form').trigger('submit')
    await flushPromises()

    expect(createMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('gesperrt')
  })
})
