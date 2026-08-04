import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import PluginsListView from './PluginsListView.vue'
import type { AdminContext } from '@/core/auth/adminContext'
import type { PluginInstallation } from './pluginsApi'
import { registerHook, resetHooks } from '@/core/extensions/hooks'
import { registerService, resetServices } from '@/core/extensions/services'

const {
  listMock,
  activateMock,
  deactivateMock,
  installLocalMock,
  uninstallMock,
  signatureReportMock,
  contextRef,
  uiLoadResultsRef,
} = vi.hoisted(() => ({
  listMock: vi.fn(),
  activateMock: vi.fn(),
  deactivateMock: vi.fn(),
  installLocalMock: vi.fn(),
  uninstallMock: vi.fn(),
  signatureReportMock: vi.fn(),
  contextRef: { value: null as AdminContext | null },
  uiLoadResultsRef: {
    value: [] as Array<{ pluginId: string; url: string; status: string; detail?: string }>,
  },
}))

vi.mock('@/core/extensions/loader', () => ({ getPluginUiLoadResults: () => uiLoadResultsRef.value }))

vi.mock('./pluginsApi', () => ({
  isPluginActive: (state: number) => state === 1,
  pluginsApi: {
    list: listMock,
    activate: activateMock,
    deactivate: deactivateMock,
    installLocal: installLocalMock,
    uninstall: uninstallMock,
    signatureReport: signatureReportMock,
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

const sample: PluginInstallation[] = [
  { pluginId: 'acme', displayName: 'Acme', assemblyPath: '', entryTypeName: null, state: 1, installedAtUtc: '', updatedAtUtc: '' },
  { pluginId: 'beta', displayName: 'Beta', assemblyPath: '', entryTypeName: null, state: 2, installedAtUtc: '', updatedAtUtc: '' },
]

function buttonByText(wrapper: VueWrapper, text: string) {
  return wrapper.findAll('button').find((b) => b.text().includes(text))
}

const okResult = { isSuccess: true, pluginId: null, message: null, errorCode: null, warningMessage: null, warningCode: null }

beforeEach(() => {
  for (const m of [listMock, activateMock, deactivateMock, installLocalMock, uninstallMock, signatureReportMock]) {
    m.mockReset()
  }
  listMock.mockResolvedValue(sample)
  activateMock.mockResolvedValue(okResult)
  deactivateMock.mockResolvedValue(okResult)
  installLocalMock.mockResolvedValue(okResult)
  uninstallMock.mockResolvedValue(okResult)
  signatureReportMock.mockResolvedValue([])
  uiLoadResultsRef.value = []
  resetHooks()
  resetServices()
})

describe('PluginsListView', () => {
  it('renders each plugin with the right status and lifecycle action', async () => {
    contextRef.value = ctx(['plugin.execute'])
    const wrapper = mount(PluginsListView)
    await flushPromises()

    const text = wrapper.text()
    expect(text).toContain('Acme')
    expect(text).toContain('Beta')
    // Active plugin offers Deactivate, inactive one offers Activate.
    expect(text).toContain('Deaktivieren')
    expect(text).toContain('Aktivieren')
  })

  it('renders a signature badge per plugin from the report', async () => {
    contextRef.value = ctx(['plugin.read'])
    signatureReportMock.mockResolvedValue([
      { pluginId: 'acme', state: 'signed-trusted', signerFingerprint: 'FP' },
      { pluginId: 'beta', state: 'unsigned', signerFingerprint: null },
    ])
    const wrapper = mount(PluginsListView)
    await flushPromises()

    const text = wrapper.text()
    expect(text).toContain('Signiert') // acme → signed-trusted
    expect(text).toContain('Unsigniert') // beta → unsigned
  })

  it('tolerates a failing signature report without breaking the list', async () => {
    contextRef.value = ctx(['plugin.read'])
    signatureReportMock.mockRejectedValue(new Error('nope'))
    const wrapper = mount(PluginsListView)
    await flushPromises()

    expect(wrapper.text()).toContain('Acme') // list still renders
  })

  it('hides all mutating actions without permissions', async () => {
    contextRef.value = ctx(['plugin.read'])
    const wrapper = mount(PluginsListView)
    await flushPromises()

    expect(buttonByText(wrapper, 'Aktivieren')).toBeUndefined()
    expect(buttonByText(wrapper, 'Deaktivieren')).toBeUndefined()
    expect(buttonByText(wrapper, 'Deinstallieren')).toBeUndefined()
    expect(wrapper.find('input[name="installId"]').exists()).toBe(false)
  })

  it('activates the inactive plugin and reloads', async () => {
    contextRef.value = ctx(['plugin.execute'])
    const wrapper = mount(PluginsListView)
    await flushPromises()

    await buttonByText(wrapper, 'Aktivieren')!.trigger('click')
    await flushPromises()

    expect(activateMock).toHaveBeenCalledWith('beta')
    expect(listMock).toHaveBeenCalledTimes(2) // initial load + reload after activate
  })

  it('aborts a lifecycle action when a before-hook cancels', async () => {
    contextRef.value = ctx(['plugin.execute'])
    registerHook('plugins.before-deactivate', (h) => h.cancel('vom Plugin gesperrt'))
    const wrapper = mount(PluginsListView)
    await flushPromises()

    await buttonByText(wrapper, 'Deaktivieren')!.trigger('click')
    await flushPromises()

    expect(deactivateMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('vom Plugin gesperrt')
  })

  it('installs the entered plugin id from local source', async () => {
    contextRef.value = ctx(['plugin.create'])
    const wrapper = mount(PluginsListView)
    await flushPromises()

    await wrapper.find('input[name="installId"]').setValue('gamma')
    await wrapper.find('form.plugins__form').trigger('submit.prevent')
    await flushPromises()

    expect(installLocalMock).toHaveBeenCalledWith('gamma', true)
  })

  it('lets a before-install hook mutate buildIfNeeded before the API call', async () => {
    contextRef.value = ctx(['plugin.create'])
    registerHook<{ buildIfNeeded: boolean }>('plugins.before-install', (h) => {
      h.payload.buildIfNeeded = false
    })
    const wrapper = mount(PluginsListView)
    await flushPromises()

    await wrapper.find('input[name="installId"]').setValue('gamma')
    await wrapper.find('form.plugins__form').trigger('submit.prevent')
    await flushPromises()

    expect(installLocalMock).toHaveBeenCalledWith('gamma', false)
  })

  it('aborts install when a before-install hook cancels', async () => {
    contextRef.value = ctx(['plugin.create'])
    registerHook('plugins.before-install', (h) => h.cancel('Installation blockiert'))
    const wrapper = mount(PluginsListView)
    await flushPromises()

    await wrapper.find('input[name="installId"]').setValue('gamma')
    await wrapper.find('form.plugins__form').trigger('submit.prevent')
    await flushPromises()

    expect(installLocalMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Installation blockiert')
  })

  it('surfaces a lifecycle error message', async () => {
    contextRef.value = ctx(['plugin.execute'])
    activateMock.mockRejectedValueOnce(new Error('Abhängigkeit fehlt'))
    const wrapper = mount(PluginsListView)
    await flushPromises()

    await buttonByText(wrapper, 'Aktivieren')!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Abhängigkeit fehlt')
  })

  it('surfaces a service override conflict in the diagnostics section', async () => {
    contextRef.value = ctx(['plugin.read'])
    // Two plugins claim the same service → the registry records a conflict.
    registerService('usersApi', { name: 'a' }, { pluginId: 'alpha', priority: 1 })
    registerService('usersApi', { name: 'b' }, { pluginId: 'beta', priority: 5 })
    const wrapper = mount(PluginsListView)
    await flushPromises()

    const text = wrapper.text()
    expect(text).toContain('Service-Konflikte')
    expect(text).toContain('usersApi')
    expect(text).toContain('beta') // active owner (higher priority)
    expect(text).toContain('alpha') // shadowed owner
  })

  it('surfaces a failed plugin UI load in the diagnostics section', async () => {
    contextRef.value = ctx(['plugin.read'])
    uiLoadResultsRef.value = [
      { pluginId: 'communication', url: '/plugin-assets/communication/admin.js', status: 'failed', detail: 'SyntaxError' },
    ]
    const wrapper = mount(PluginsListView)
    await flushPromises()

    const text = wrapper.text()
    expect(text).toContain('Fehlgeschlagene Plugin-UIs')
    expect(text).toContain('communication')
    expect(text).toContain('SyntaxError')
  })
})
