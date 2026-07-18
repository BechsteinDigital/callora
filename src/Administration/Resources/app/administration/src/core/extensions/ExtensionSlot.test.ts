import { describe, it, expect, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import ExtensionSlot from './ExtensionSlot.vue'
import { registerExtension, resetExtensions } from './registry'

beforeEach(() => resetExtensions())

describe('ExtensionSlot', () => {
  it('renders nothing for an empty slot', () => {
    const wrapper = mount(ExtensionSlot, { props: { name: 'users.detail.fields' } })
    expect(wrapper.find('span').exists()).toBe(false)
  })

  it('renders registered components in order and passes ctx through', () => {
    const A = defineComponent({ props: ['ctx'], setup: (p) => () => h('span', { class: 'a' }, `A:${p.ctx}`) })
    const B = defineComponent({ setup: () => () => h('span', { class: 'b' }, 'B') })
    registerExtension('users.detail.fields', B, 10)
    registerExtension('users.detail.fields', A, 1) // lower order → rendered first

    const wrapper = mount(ExtensionSlot, { props: { name: 'users.detail.fields', ctx: 'u1' } })
    const spans = wrapper.findAll('span')

    expect(spans).toHaveLength(2)
    expect(spans[0].classes()).toContain('a')
    expect(spans[0].text()).toBe('A:u1') // ctx reached the extension
  })

  it('ignores components registered for other slots', () => {
    const A = defineComponent({ setup: () => () => h('span', 'A') })
    registerExtension('other.slot', A)

    const wrapper = mount(ExtensionSlot, { props: { name: 'users.detail.fields' } })
    expect(wrapper.findAll('span')).toHaveLength(0)
  })
})
