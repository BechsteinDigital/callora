import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import ThemeSettings from './ThemeSettings.vue'
import type { ThemeSettingDefinition, ThemeSettings as ThemeSettingsData } from './themesApi'
import { registerHook, resetHooks } from '@/core/extensions/hooks'
import { resetServices } from '@/core/extensions/services'

const { getSettingsMock, saveSettingsMock } = vi.hoisted(() => ({
  getSettingsMock: vi.fn(),
  saveSettingsMock: vi.fn(),
}))

vi.mock('./themesApi', () => ({
  themesApi: { getSettings: getSettingsMock, saveSettings: saveSettingsMock },
}))

function field(over: Partial<ThemeSettingDefinition>): ThemeSettingDefinition {
  return {
    settingKey: 'primaryColor',
    label: 'Primärfarbe',
    fieldType: 'color',
    description: null,
    defaultValueJson: '"#000000"',
    isRequired: false,
    sortOrder: 1,
    groupName: null,
    optionsJson: null,
    isActive: true,
    ...over,
  }
}

function settings(over: Partial<ThemeSettingsData>): ThemeSettingsData {
  return {
    workspaceKey: 'workspace-a',
    hasAssignedTheme: true,
    themePluginId: 'customer.theme',
    themeVersion: '1.0.0',
    fields: [field({})],
    valuesByKey: { primaryColor: '"#ffffff"' },
    ...over,
  }
}

function mountSettings(canManage: boolean) {
  return mount(ThemeSettings, { props: { workspaceKey: 'workspace-a', canManage } })
}

beforeEach(() => {
  getSettingsMock.mockReset().mockResolvedValue(settings({}))
  saveSettingsMock.mockReset().mockResolvedValue(settings({}))
  resetHooks()
  resetServices()
})

describe('ThemeSettings', () => {
  it('renders active fields prefilled from the stored values', async () => {
    const wrapper = mountSettings(true)
    await flushPromises()

    expect(getSettingsMock).toHaveBeenCalledWith('workspace-a')
    expect(wrapper.text()).toContain('Primärfarbe')
    // The JSON string value "#ffffff" is shown as its inner text.
    expect((wrapper.find('input[name="theme-setting-primaryColor"]').element as HTMLInputElement).value).toBe('#ffffff')
  })

  it('hides inactive fields', async () => {
    getSettingsMock.mockResolvedValueOnce(
      settings({ fields: [field({}), field({ settingKey: 'hidden', label: 'Hidden', isActive: false })] }),
    )
    const wrapper = mountSettings(true)
    await flushPromises()

    expect(wrapper.find('input[name="theme-setting-hidden"]').exists()).toBe(false)
  })

  it('shows an empty state when the theme has no settings', async () => {
    getSettingsMock.mockResolvedValueOnce(settings({ fields: [], valuesByKey: {} }))
    const wrapper = mountSettings(true)
    await flushPromises()

    expect(wrapper.text()).toContain('keine Einstellungen')
    expect(wrapper.find('form.fields').exists()).toBe(false)
  })

  it('saves coerced, non-empty values', async () => {
    getSettingsMock.mockResolvedValueOnce(
      settings({
        fields: [field({}), field({ settingKey: 'maxItems', label: 'Max', fieldType: 'number', sortOrder: 2, defaultValueJson: '10' })],
        valuesByKey: { primaryColor: '"#ffffff"' },
      }),
    )
    const wrapper = mountSettings(true)
    await flushPromises()

    await wrapper.find('input[name="theme-setting-primaryColor"]').setValue('#abcabc')
    await wrapper.find('input[name="theme-setting-maxItems"]').setValue('7')
    await wrapper.find('form.fields').trigger('submit')
    await flushPromises()

    // Color stays a string, the numeric field is coerced to a JSON number.
    expect(saveSettingsMock).toHaveBeenCalledWith('workspace-a', { primaryColor: '#abcabc', maxItems: 7 })
    expect(wrapper.text()).toContain('gespeichert')
  })

  it('omits an emptied field so it falls back to the default', async () => {
    const wrapper = mountSettings(true)
    await flushPromises()

    await wrapper.find('input[name="theme-setting-primaryColor"]').setValue('')
    await wrapper.find('form.fields').trigger('submit')
    await flushPromises()

    expect(saveSettingsMock).toHaveBeenCalledWith('workspace-a', {})
  })

  it('renders read-only without manage permission (no save button)', async () => {
    const wrapper = mountSettings(false)
    await flushPromises()

    expect((wrapper.find('input[name="theme-setting-primaryColor"]').element as HTMLInputElement).disabled).toBe(true)
    expect(wrapper.find('.buttons').exists()).toBe(false)
  })

  it('aborts save when a before-save hook cancels', async () => {
    registerHook('themes.settings.before-save', (h) => h.cancel('gesperrt'))
    const wrapper = mountSettings(true)
    await flushPromises()

    await wrapper.find('form.fields').trigger('submit')
    await flushPromises()

    expect(saveSettingsMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('gesperrt')
  })
})
