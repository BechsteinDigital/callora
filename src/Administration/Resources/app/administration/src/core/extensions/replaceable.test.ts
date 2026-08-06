import { beforeEach, describe, expect, it } from 'vitest'
import { h } from 'vue'
import {
  defineReplaceable,
  getComponentConflicts,
  replaceComponent,
  resetReplacements,
  useComponent,
} from './replaceable'

const Base = { name: 'Base', setup: () => () => h('div', 'base') }
const Custom = { name: 'Custom', setup: () => () => h('div', 'custom') }
const Other = { name: 'Other', setup: () => () => h('div', 'other') }

describe('replaceable components', () => {
  beforeEach(resetReplacements)

  it('resolves to the original when nobody replaced it', () => {
    expect(useComponent(defineReplaceable('cal.data-table', Base))).toBe(Base)
  })

  it('resolves to a registered replacement', () => {
    const token = defineReplaceable('cal.data-table', Base)
    replaceComponent('cal.data-table', Custom)

    expect(useComponent(token)).toBe(Custom)
  })

  it('lets the highest priority win', () => {
    const token = defineReplaceable('cal.data-table', Base)
    replaceComponent('cal.data-table', Custom, { priority: 1, pluginId: 'a' })
    replaceComponent('cal.data-table', Other, { priority: 5, pluginId: 'b' })

    expect(useComponent(token)).toBe(Other)
  })

  it('lets the later registration win on equal priority, matching the loader order', () => {
    const token = defineReplaceable('cal.data-table', Base)
    replaceComponent('cal.data-table', Custom, { pluginId: 'a' })
    replaceComponent('cal.data-table', Other, { pluginId: 'b' })

    expect(useComponent(token)).toBe(Other)
  })

  it('keeps replacements apart per key', () => {
    const table = defineReplaceable('cal.data-table', Base)
    const dialog = defineReplaceable('cal.dialog', Base)
    replaceComponent('cal.data-table', Custom)

    expect(useComponent(table)).toBe(Custom)
    expect(useComponent(dialog)).toBe(Base)
  })

  it('reports a conflict so an operator sees which plugin was shadowed', () => {
    defineReplaceable('cal.data-table', Base)
    replaceComponent('cal.data-table', Custom, { pluginId: 'a' })
    replaceComponent('cal.data-table', Other, { pluginId: 'b' })

    expect(getComponentConflicts()).toEqual([
      { key: 'cal.data-table', activePluginId: 'b', shadowedPluginIds: ['a'] },
    ])
  })

  it('reports no conflict for a single replacement', () => {
    defineReplaceable('cal.data-table', Base)
    replaceComponent('cal.data-table', Custom, { pluginId: 'a' })

    expect(getComponentConflicts()).toEqual([])
  })

  it('carries key and fallback on the token, so a consumer cannot resolve the wrong one', () => {
    const token = defineReplaceable('cal.dialog', Base)

    expect(token.key).toBe('cal.dialog')
    expect(token.base).toBe(Base)
  })

  it('does not mutate the component it is given', () => {
    defineReplaceable('cal.dialog', Base)

    expect(Object.keys(Base)).toEqual(['name', 'setup'])
  })

  it('lets one component stand behind two keys without the second erasing the first', () => {
    const asTable = defineReplaceable('cal.data-table', Base)
    const asDialog = defineReplaceable('cal.dialog', Base)
    replaceComponent('cal.dialog', Custom)

    expect(useComponent(asTable)).toBe(Base)
    expect(useComponent(asDialog)).toBe(Custom)
  })

  it('renders the replacement, not just resolves it', async () => {
    const { mount } = await import('@vue/test-utils')
    const token = defineReplaceable('cal.data-table', Base)
    replaceComponent('cal.data-table', Custom)

    const wrapper = mount({ setup: () => () => h(useComponent(token)) })

    expect(wrapper.text()).toBe('custom')
  })
})
