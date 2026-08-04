import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import WorkspacePlugins from './WorkspacePlugins.vue'
import { resetServices } from '@/core/extensions/services'

const { listPluginsMock, setPluginAssignmentMock } = vi.hoisted(() => ({
  listPluginsMock: vi.fn(),
  setPluginAssignmentMock: vi.fn(),
}))

vi.mock('./workspacesApi', () => ({
  workspacesApi: {
    listPlugins: listPluginsMock,
    setPluginAssignment: setPluginAssignmentMock,
  },
}))

const activePlugin = {
  pluginId: 'videoconference',
  displayName: 'Video Conference',
  isGloballyActive: true,
  isEntitled: false,
  isActive: false,
  isAssigned: false,
}

beforeEach(() => {
  listPluginsMock.mockReset()
  setPluginAssignmentMock.mockReset()
  listPluginsMock.mockResolvedValue([activePlugin])
  resetServices()
})

describe('WorkspacePlugins', () => {
  it('lists installed plugins and exposes assignment state', async () => {
    const wrapper = mount(WorkspacePlugins, {
      props: { workspaceKey: 'acme', canManage: true },
    })
    await flushPromises()

    expect(listPluginsMock).toHaveBeenCalledWith('acme')
    expect(wrapper.text()).toContain('Video Conference')
    expect(wrapper.text()).toContain('Nicht zugewiesen')
    expect(wrapper.get('[data-testid="plugin-assignment-videoconference"]').text()).toContain(
      'Zuweisen',
    )
  })

  it('assigns a plugin and updates the rendered state', async () => {
    setPluginAssignmentMock.mockResolvedValue({
      ...activePlugin,
      isEntitled: true,
      isActive: true,
      isAssigned: true,
    })
    const wrapper = mount(WorkspacePlugins, {
      props: { workspaceKey: 'acme', canManage: true },
    })
    await flushPromises()

    await wrapper.get('[data-testid="plugin-assignment-videoconference"]').trigger('click')
    await flushPromises()

    expect(setPluginAssignmentMock).toHaveBeenCalledWith('acme', 'videoconference', true)
    expect(wrapper.text()).toContain('Zugewiesen')
    expect(wrapper.get('[data-testid="plugin-assignment-videoconference"]').text()).toContain(
      'Entfernen',
    )
  })

  it('shows a dependency rejection returned by the backend', async () => {
    setPluginAssignmentMock.mockRejectedValueOnce(
      new Error("Required capability 'communication.foundation' is missing."),
    )
    const wrapper = mount(WorkspacePlugins, {
      props: { workspaceKey: 'acme', canManage: true },
    })
    await flushPromises()

    await wrapper.get('[data-testid="plugin-assignment-videoconference"]').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain("Required capability 'communication.foundation' is missing.")
  })

  it('hides mutation controls without plugin.execute permission', async () => {
    const wrapper = mount(WorkspacePlugins, {
      props: { workspaceKey: 'acme', canManage: false },
    })
    await flushPromises()

    expect(wrapper.find('[data-testid="plugin-assignment-videoconference"]').exists()).toBe(false)
  })

  it('disables assignment while the plugin is globally inactive', async () => {
    listPluginsMock.mockResolvedValue([{ ...activePlugin, isGloballyActive: false }])
    const wrapper = mount(WorkspacePlugins, {
      props: { workspaceKey: 'acme', canManage: true },
    })
    await flushPromises()

    expect(
      wrapper.get<HTMLButtonElement>('[data-testid="plugin-assignment-videoconference"]')
        .element.disabled,
    ).toBe(true)
    expect(wrapper.text()).toContain('global aktiviert werden')
  })
})
