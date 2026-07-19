import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import TenantsListView from './TenantsListView.vue'
import type { AdminContext } from '@/core/auth/adminContext'
import type { Tenant } from './tenantsApi'

const { listMock, createMock, activateMock, suspendMock, removeMock, contextRef } = vi.hoisted(() => ({
  listMock: vi.fn(),
  createMock: vi.fn(),
  activateMock: vi.fn(),
  suspendMock: vi.fn(),
  removeMock: vi.fn(),
  contextRef: { value: null as AdminContext | null },
}))

vi.mock('./tenantsApi', () => ({
  tenantsApi: {
    list: listMock,
    create: createMock,
    activate: activateMock,
    suspend: suspendMock,
    remove: removeMock,
  },
}))
vi.mock('@/core/auth/authStore', () => ({ useAuthStore: () => ({ context: contextRef }) }))

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

const sampleTenants: Tenant[] = [
  { tenantKey: 'acme', displayName: 'Acme', isActive: true, createdAtUtc: '', updatedAtUtc: '' },
  { tenantKey: 'globex', displayName: 'Globex', isActive: false, createdAtUtc: '', updatedAtUtc: '' },
]

beforeEach(() => {
  listMock.mockReset().mockResolvedValue(sampleTenants)
  createMock.mockReset().mockResolvedValue(sampleTenants[0])
  activateMock.mockReset().mockResolvedValue(undefined)
  suspendMock.mockReset().mockResolvedValue(undefined)
  removeMock.mockReset().mockResolvedValue(undefined)
})

describe('TenantsListView', () => {
  it('renders tenants with their status and full management actions for a super admin', async () => {
    contextRef.value = ctx(['*'])
    const wrapper = mount(TenantsListView)
    await flushPromises()

    const text = wrapper.text()
    expect(text).toContain('acme')
    expect(text).toContain('Globex')
    expect(text).toContain('Aktiv')
    expect(text).toContain('Suspendiert')
    // Create form present, one Suspendieren (active row) + one Aktivieren (inactive row).
    expect(wrapper.find('form.create').exists()).toBe(true)
    expect(text).toContain('Suspendieren')
    expect(text).toContain('Aktivieren')
    expect(wrapper.findAll('.link-danger')).toHaveLength(2)
  })

  it('hides create form and management actions without the tenant permissions', async () => {
    contextRef.value = ctx(['tenant.read'])
    const wrapper = mount(TenantsListView)
    await flushPromises()

    expect(wrapper.find('form.create').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('Suspendieren')
    expect(wrapper.text()).not.toContain('Aktivieren')
    expect(wrapper.findAll('.link-danger')).toHaveLength(0)
  })

  it('creates a tenant from the inline form and reloads', async () => {
    contextRef.value = ctx(['*'])
    const wrapper = mount(TenantsListView)
    await flushPromises()

    await wrapper.find('input[name="tenantKey"]').setValue('initech')
    await wrapper.find('input[name="displayName"]').setValue('Initech')
    await wrapper.find('form.create').trigger('submit')
    await flushPromises()

    expect(createMock).toHaveBeenCalledWith('initech', 'Initech')
    expect(listMock).toHaveBeenCalledTimes(2) // initial load + reload after create
  })

  it('suspends an active tenant', async () => {
    contextRef.value = ctx(['*'])
    const wrapper = mount(TenantsListView)
    await flushPromises()

    const suspend = wrapper.findAll('button.link').find((b) => b.text() === 'Suspendieren')
    await suspend!.trigger('click')
    await flushPromises()

    expect(suspendMock).toHaveBeenCalledWith('acme')
  })

  it('activates a suspended tenant', async () => {
    contextRef.value = ctx(['*'])
    const wrapper = mount(TenantsListView)
    await flushPromises()

    const activate = wrapper.findAll('button.link').find((b) => b.text() === 'Aktivieren')
    await activate!.trigger('click')
    await flushPromises()

    expect(activateMock).toHaveBeenCalledWith('globex')
  })

  it('deletes only after confirmation', async () => {
    contextRef.value = ctx(['*'])
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false)
    const wrapper = mount(TenantsListView)
    await flushPromises()

    await wrapper.find('.link-danger').trigger('click')
    await flushPromises()
    expect(removeMock).not.toHaveBeenCalled()

    confirmSpy.mockReturnValue(true)
    await wrapper.find('.link-danger').trigger('click')
    await flushPromises()
    expect(removeMock).toHaveBeenCalledWith('acme')

    confirmSpy.mockRestore()
  })
})
