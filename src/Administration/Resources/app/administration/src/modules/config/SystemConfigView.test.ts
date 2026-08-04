import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import SystemConfigView from './SystemConfigView.vue'
import type { AdminContext } from '@/core/auth/adminContext'
import type { ConfigDefinition } from './systemConfigApi'
import { registerHook, resetHooks } from '@/core/extensions/hooks'
import { resetServices } from '@/core/extensions/services'

const { listMock, effectiveMock, saveMock, contextRef } = vi.hoisted(() => ({
  listMock: vi.fn(),
  effectiveMock: vi.fn(),
  saveMock: vi.fn(),
  contextRef: { value: null as AdminContext | null },
}))

vi.mock('./systemConfigApi', () => ({
  isSecretField: (ft: string) => ft === 'secret',
  ConfigScope: { Global: 'global', Tenant: 'tenant', Workspace: 'workspace' },
  systemConfigApi: { listDefinitions: listMock, effective: effectiveMock, saveValues: saveMock },
}))
vi.mock('@/core/auth/authStore', () => ({ useAuthStore: () => ({ context: contextRef }) }))
// The workspace scope reads the shell's active workspace; this suite exercises
// the platform scope, so the context is stubbed as "no workspace resolved".
vi.mock('@/core/workspace/workspaceContext', async () => {
  const { computed, ref } = await import('vue')
  const activeWorkspace = ref('')
  return {
    useWorkspaceContext: () => ({
      activeWorkspace: computed(() => activeWorkspace.value),
      ensure: () => Promise.resolve(),
    }),
  }
})
vi.mock('@/modules/tenants/tenantsApi', () => ({ tenantsApi: { list: vi.fn().mockResolvedValue([]) } }))

function ctx(permissions: string[]): AdminContext {
  return {
    userId: 'u',
    displayName: null,
    email: null,
    roles: [],
    permissions,
    scope: 'platform',
    workspaceKey: null,
    // Operators see all three scopes and start on the platform level — the view
    // these assertions describe.
    isOperator: true,
  }
}

function def(over: Partial<ConfigDefinition>): ConfigDefinition {
  return {
    pluginId: 'acme',
    version: '1.0',
    configKey: 'greeting',
    label: 'Begrüßung',
    fieldType: 'text',
    description: null,
    defaultValueJson: null,
    groupName: null,
    optionsJson: null,
    sortOrder: 0,
    isActive: true,
    ...over,
  }
}

const defs: ConfigDefinition[] = [
  def({ configKey: 'greeting', fieldType: 'text', sortOrder: 0 }),
  def({ configKey: 'apiKey', label: 'API-Schlüssel', fieldType: 'secret', sortOrder: 1 }),
  def({ pluginId: 'beta', configKey: 'x', fieldType: 'text' }),
]

beforeEach(() => {
  listMock.mockReset().mockResolvedValue(defs)
  effectiveMock.mockReset().mockResolvedValue({
    pluginId: 'acme',
    workspaceKey: null,
    valuesByKey: { greeting: '"hi"', apiKey: '"***"' },
  })
  saveMock.mockReset().mockResolvedValue(undefined)
  resetHooks()
  resetServices()
})

describe('SystemConfigView', () => {
  it('renders the first plugin fields with effective references', async () => {
    contextRef.value = ctx(['config.update'])
    const wrapper = mount(SystemConfigView)
    await flushPromises()

    expect(wrapper.find('input[name="greeting"]').exists()).toBe(true)
    expect(wrapper.find('input[name="apiKey"]').attributes('type')).toBe('password')
    expect(wrapper.text()).toContain('hi') // decoded effective value
    expect(wrapper.text()).toContain('•••• (gesetzt)') // secret reference
  })

  it('disables editing and hides save without config.update', async () => {
    contextRef.value = ctx(['config.read'])
    const wrapper = mount(SystemConfigView)
    await flushPromises()

    expect(wrapper.find('input[name="greeting"]').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).not.toContain('Speichern')
  })

  it('saves only entered values, coercing non-secret input to JSON', async () => {
    contextRef.value = ctx(['config.update'])
    const wrapper = mount(SystemConfigView)
    await flushPromises()

    await wrapper.find('input[name="greeting"]').setValue('42')
    await wrapper.find('form.config__fields').trigger('submit.prevent')
    await flushPromises()

    expect(saveMock).toHaveBeenCalledWith('acme', 'global', null, { greeting: 42 })
  })

  it('sends a secret as its plaintext string and omits blank fields', async () => {
    contextRef.value = ctx(['config.update'])
    const wrapper = mount(SystemConfigView)
    await flushPromises()

    await wrapper.find('input[name="apiKey"]').setValue('s3cr3t')
    await wrapper.find('form.config__fields').trigger('submit.prevent')
    await flushPromises()

    expect(saveMock).toHaveBeenCalledWith('acme', 'global', null, { apiKey: 's3cr3t' })
  })

  it('does not call save when nothing was entered', async () => {
    contextRef.value = ctx(['config.update'])
    const wrapper = mount(SystemConfigView)
    await flushPromises()

    await wrapper.find('form.config__fields').trigger('submit.prevent')
    await flushPromises()

    expect(saveMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Keine Änderungen')
  })

  it('aborts save when a before-save hook cancels', async () => {
    contextRef.value = ctx(['config.update'])
    registerHook('config.before-save', (h) => h.cancel('vom Plugin gesperrt'))
    const wrapper = mount(SystemConfigView)
    await flushPromises()

    await wrapper.find('input[name="greeting"]').setValue('hi')
    await wrapper.find('form.config__fields').trigger('submit.prevent')
    await flushPromises()

    expect(saveMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('vom Plugin gesperrt')
  })

  it('lets a before-save hook add a value to the payload', async () => {
    contextRef.value = ctx(['config.update'])
    registerHook<{ values: Record<string, unknown> }>('config.before-save', (h) => {
      h.payload.values.injected = true
    })
    const wrapper = mount(SystemConfigView)
    await flushPromises()

    await wrapper.find('input[name="greeting"]').setValue('hi')
    await wrapper.find('form.config__fields').trigger('submit.prevent')
    await flushPromises()

    expect(saveMock).toHaveBeenCalledWith('acme', 'global', null, expect.objectContaining({ injected: true }))
  })

  it('reloads effective and resets inputs when switching plugins', async () => {
    contextRef.value = ctx(['config.update'])
    const wrapper = mount(SystemConfigView)
    await flushPromises()

    await wrapper.find('input[name="greeting"]').setValue('foo')
    await wrapper.find('select[name="plugin"]').setValue('beta')
    await flushPromises()
    expect(effectiveMock).toHaveBeenLastCalledWith('beta', { tenantKey: undefined, workspaceKey: undefined })

    // Switching back must not carry the previous plugin's entry over.
    await wrapper.find('select[name="plugin"]').setValue('acme')
    await flushPromises()
    expect((wrapper.find('input[name="greeting"]').element as HTMLInputElement).value).toBe('')
  })
})
