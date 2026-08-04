import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import SurfaceThemeDialog from './SurfaceThemeDialog.vue'
import { resetServices } from '@/core/extensions/services'
import type { SurfaceThemeAssignment, SurfaceThemeSettings, ThemeDefinition, ThemeSettingDefinition } from './themesApi'

const { listDefsMock, getAssignmentMock, getSettingsMock, assignMock, clearMock, saveMock } = vi.hoisted(() => ({
  listDefsMock: vi.fn(),
  getAssignmentMock: vi.fn(),
  getSettingsMock: vi.fn(),
  assignMock: vi.fn(),
  clearMock: vi.fn(),
  saveMock: vi.fn(),
}))

vi.mock('./themesApi', () => ({
  themesApi: {
    listDefinitions: listDefsMock,
    getSurfaceAssignment: getAssignmentMock,
    getSurfaceSettings: getSettingsMock,
    assignSurface: assignMock,
    clearSurfaceAssignment: clearMock,
    saveSurfaceSettings: saveMock,
  },
}))

function field(over: Partial<ThemeSettingDefinition> = {}): ThemeSettingDefinition {
  return {
    settingKey: 'primary.color',
    label: 'Primärfarbe',
    fieldType: 'color',
    description: null,
    defaultValueJson: '"#000000"',
    isRequired: false,
    sortOrder: 0,
    groupName: null,
    optionsJson: null,
    isActive: true,
    ...over,
  }
}

function definition(): ThemeDefinition {
  return {
    templateKey: 'workspace.alpha',
    surface: 'workspace',
    pluginId: 'theme-alpha',
    version: '1.0.0',
    displayName: 'Alpha',
    templatePath: 'x.html',
    parentTemplateKey: null,
    scope: 'workspace',
    isActive: true,
    priority: 100,
    createdAtUtc: '2026-08-01T00:00:00Z',
    updatedAtUtc: '2026-08-01T00:00:00Z',
  }
}

function assignment(over: Partial<SurfaceThemeAssignment> = {}): SurfaceThemeAssignment {
  return {
    workspaceKey: 'ws',
    surfaceKey: 'shop',
    themePluginId: 'theme-alpha',
    themeVersion: '1.0.0',
    inheritedFromWorkspace: true,
    ...over,
  }
}

function settings(over: Partial<SurfaceThemeSettings> = {}): SurfaceThemeSettings {
  return {
    workspaceKey: 'ws',
    surfaceKey: 'shop',
    hasAssignedTheme: true,
    themePluginId: 'theme-alpha',
    themeVersion: '1.0.0',
    inheritedFromWorkspace: true,
    inheritsWorkspaceValues: true,
    fields: [field()],
    valuesByKey: {},
    inheritedValuesByKey: {},
    ...over,
  }
}

function mountDialog() {
  return mount(SurfaceThemeDialog, {
    props: { open: true, workspaceKey: 'ws', surfaceKey: 'shop', canManage: true },
    global: { stubs: { DialogPortal: { template: '<div><slot /></div>' } } },
  })
}

beforeEach(() => {
  resetServices()
  listDefsMock.mockReset().mockResolvedValue([definition()])
  getAssignmentMock.mockReset().mockResolvedValue(assignment())
  getSettingsMock.mockReset().mockResolvedValue(settings())
  assignMock.mockReset().mockResolvedValue(assignment({ inheritedFromWorkspace: false }))
  clearMock.mockReset().mockResolvedValue(assignment())
  saveMock.mockReset().mockResolvedValue(settings())
})

describe('SurfaceThemeDialog', () => {
  it('shows that the surface follows its workspace', async () => {
    const wrapper = mountDialog()
    await flushPromises()

    expect(wrapper.text()).toContain('vom Workspace geerbt')
  })

  it('prefills only the surface values, never the inherited ones', async () => {
    // Prefilling an inherited value would silently copy it onto the surface on
    // the next save, turning an inherited setting into an override.
    getSettingsMock.mockResolvedValue(
      settings({ inheritedValuesByKey: { 'primary.color': '"#336699"' }, valuesByKey: {} }),
    )
    const wrapper = mountDialog()
    await flushPromises()

    const input = wrapper.find<HTMLInputElement>('input[name="surface-setting-primary.color"]')
    expect(input.element.value).toBe('')
    expect(input.attributes('placeholder')).toBe('#336699')
  })

  it('prefills a value the surface actually overrides', async () => {
    getSettingsMock.mockResolvedValue(settings({ valuesByKey: { 'primary.color': '"#ff0000"' } }))
    const wrapper = mountDialog()
    await flushPromises()

    expect(wrapper.find<HTMLInputElement>('input[name="surface-setting-primary.color"]').element.value).toBe('#ff0000')
  })

  it('falls back to the theme default when nothing is inherited', async () => {
    const wrapper = mountDialog()
    await flushPromises()

    expect(
      wrapper.find('input[name="surface-setting-primary.color"]').attributes('placeholder'),
    ).toBe('#000000')
  })

  it('saves only the fields that were filled in', async () => {
    getSettingsMock.mockResolvedValue(
      settings({ fields: [field(), field({ settingKey: 'logo.text', label: 'Logo' })] }),
    )
    const wrapper = mountDialog()
    await flushPromises()

    await wrapper.find('input[name="surface-setting-primary.color"]').setValue('#ff0000')
    await wrapper.findAll('button').find((b) => b.text().includes('Werte speichern'))!.trigger('click')
    await flushPromises()

    expect(saveMock).toHaveBeenCalledWith('ws', 'shop', { 'primary.color': '#ff0000' })
  })

  it('drops an override by emptying its field', async () => {
    getSettingsMock.mockResolvedValue(settings({ valuesByKey: { 'primary.color': '"#ff0000"' } }))
    const wrapper = mountDialog()
    await flushPromises()

    await wrapper.find('input[name="surface-setting-primary.color"]').setValue('')
    await wrapper.findAll('button').find((b) => b.text().includes('Werte speichern'))!.trigger('click')
    await flushPromises()

    expect(saveMock).toHaveBeenCalledWith('ws', 'shop', {})
  })

  it('assigns the picked theme to the surface', async () => {
    const wrapper = mountDialog()
    await flushPromises()

    await wrapper.find('select[name="surfaceTheme"]').setValue('theme-alpha@1.0.0')
    await wrapper.findAll('button').find((b) => b.text().includes('Theme übernehmen'))!.trigger('click')
    await flushPromises()

    expect(assignMock).toHaveBeenCalledWith('ws', 'shop', 'theme-alpha', '1.0.0')
  })

  it('returns the surface to the workspace theme when the empty option is picked', async () => {
    getAssignmentMock.mockResolvedValue(assignment({ inheritedFromWorkspace: false }))
    const wrapper = mountDialog()
    await flushPromises()

    await wrapper.find('select[name="surfaceTheme"]').setValue('')
    await wrapper.findAll('button').find((b) => b.text().includes('Theme übernehmen'))!.trigger('click')
    await flushPromises()

    expect(clearMock).toHaveBeenCalledWith('ws', 'shop')
  })

  it('explains why a differing theme inherits nothing', async () => {
    getAssignmentMock.mockResolvedValue(assignment({ inheritedFromWorkspace: false, themePluginId: 'theme-beta' }))
    getSettingsMock.mockResolvedValue(
      settings({ inheritedFromWorkspace: false, inheritsWorkspaceValues: false, themePluginId: 'theme-beta' }),
    )
    const wrapper = mountDialog()
    await flushPromises()

    expect(wrapper.text()).toContain('nicht vererbt')
  })

  it('surfaces a failed load instead of showing an empty editor', async () => {
    getSettingsMock.mockRejectedValueOnce(new Error('Forbidden.'))
    const wrapper = mountDialog()
    await flushPromises()

    expect(wrapper.text()).toContain('Forbidden.')
  })

  it('locks the editor for a caller who may not manage themes', async () => {
    const wrapper = mount(SurfaceThemeDialog, {
      props: { open: true, workspaceKey: 'ws', surfaceKey: 'shop', canManage: false },
      global: { stubs: { DialogPortal: { template: '<div><slot /></div>' } } },
    })
    await flushPromises()

    expect(
      wrapper.find<HTMLInputElement>('input[name="surface-setting-primary.color"]').element.disabled,
    ).toBe(true)
    expect(wrapper.findAll('button').some((b) => b.text().includes('Werte speichern'))).toBe(false)
  })
})
