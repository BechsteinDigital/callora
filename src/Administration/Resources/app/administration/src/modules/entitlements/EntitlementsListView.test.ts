import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import EntitlementsListView from './EntitlementsListView.vue'
import type { AdminContext } from '@/core/auth/adminContext'
import type { Entitlement } from './entitlementsApi'
import { registerHook, resetHooks } from '@/core/extensions/hooks'
import { resetServices } from '@/core/extensions/services'

const { listMock, setMock, contextRef } = vi.hoisted(() => ({
  listMock: vi.fn(),
  setMock: vi.fn(),
  contextRef: { value: null as AdminContext | null },
}))

vi.mock('./entitlementsApi', () => ({
  entitlementsApi: { list: listMock, set: setMock },
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

function entitlement(over: Partial<Entitlement>): Entitlement {
  return {
    pluginId: 'acme.plugin',
    workspaceKey: null,
    tenantKey: null,
    isEntitled: true,
    source: 'marketplace',
    createdAtUtc: '',
    updatedAtUtc: '',
    ...over,
  }
}

beforeEach(() => {
  listMock.mockReset().mockResolvedValue([entitlement({})])
  setMock.mockReset().mockResolvedValue(undefined)
  resetHooks()
  resetServices()
})

describe('EntitlementsListView', () => {
  it('lists entitlements with plugin, scope, status and source', async () => {
    contextRef.value = ctx(['*'])
    listMock.mockResolvedValueOnce([entitlement({ tenantKey: 'tenant-a', workspaceKey: null })])
    const wrapper = mount(EntitlementsListView)
    await flushPromises()

    const text = wrapper.text()
    expect(text).toContain('acme.plugin')
    expect(text).toContain('Tenant: tenant-a')
    expect(text).toContain('Berechtigt')
    expect(text).toContain('marketplace')
  })

  it('hides the grant form and toggle without plugin.execute', async () => {
    contextRef.value = ctx(['plugin.read'])
    const wrapper = mount(EntitlementsListView)
    await flushPromises()

    expect(wrapper.find('form.entitlements__form').exists()).toBe(false)
    expect(wrapper.find('button.is-ghost').exists()).toBe(false)
  })

  it('grants a plugin for a tenant scope from the form (empty workspace → null)', async () => {
    contextRef.value = ctx(['*'])
    const wrapper = mount(EntitlementsListView)
    await flushPromises()

    await wrapper.find('input[name="pluginId"]').setValue('new.plugin')
    await wrapper.find('input[name="tenantKey"]').setValue('tenant-a')
    await wrapper.find('form.entitlements__form').trigger('submit')
    await flushPromises()

    expect(setMock).toHaveBeenCalledWith({
      pluginId: 'new.plugin',
      tenantKey: 'tenant-a',
      workspaceKey: null,
      isEntitled: true,
    })
    expect(listMock).toHaveBeenCalledTimes(2) // initial + reload
  })

  it('does not grant without a plugin id', async () => {
    contextRef.value = ctx(['*'])
    const wrapper = mount(EntitlementsListView)
    await flushPromises()

    await wrapper.find('form.entitlements__form').trigger('submit')
    await flushPromises()

    expect(setMock).not.toHaveBeenCalled()
  })

  it('revokes an entitled row via the toggle', async () => {
    contextRef.value = ctx(['*'])
    listMock.mockResolvedValueOnce([entitlement({ isEntitled: true })])
    const wrapper = mount(EntitlementsListView)
    await flushPromises()

    await wrapper.find('button.is-ghost').trigger('click') // "Entziehen"
    await flushPromises()

    expect(setMock).toHaveBeenCalledWith({
      pluginId: 'acme.plugin',
      tenantKey: null,
      workspaceKey: null,
      isEntitled: false,
    })
  })

  it('grants a revoked row via the toggle', async () => {
    contextRef.value = ctx(['*'])
    listMock.mockResolvedValueOnce([entitlement({ isEntitled: false })])
    const wrapper = mount(EntitlementsListView)
    await flushPromises()

    await wrapper.find('button.is-ghost').trigger('click') // "Erteilen"
    await flushPromises()

    expect(setMock).toHaveBeenCalledWith({
      pluginId: 'acme.plugin',
      tenantKey: null,
      workspaceKey: null,
      isEntitled: true,
    })
  })

  it('aborts a grant when a before-grant hook cancels', async () => {
    contextRef.value = ctx(['*'])
    registerHook('entitlements.before-grant', (h) => h.cancel('gesperrt'))
    const wrapper = mount(EntitlementsListView)
    await flushPromises()

    await wrapper.find('input[name="pluginId"]').setValue('new.plugin')
    await wrapper.find('form.entitlements__form').trigger('submit')
    await flushPromises()

    expect(setMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('gesperrt')
  })
})
