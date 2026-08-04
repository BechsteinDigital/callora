import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { RouterLink } from 'vue-router'
import { Plus } from 'lucide-vue-next'
import CalButton from './CalButton.vue'

const routerStubs = { RouterLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

describe('CalButton', () => {
  it('renders a button by default', () => {
    const wrapper = mount(CalButton, { slots: { default: 'Speichern' } })

    expect(wrapper.element.tagName).toBe('BUTTON')
    expect(wrapper.attributes('type')).toBe('button')
    expect(wrapper.text()).toBe('Speichern')
  })

  it('becomes a router link when given a target, without a type attribute', () => {
    const wrapper = mount(CalButton, {
      props: { to: '/users/new' },
      slots: { default: 'Neu' },
      global: { stubs: routerStubs },
    })

    expect(wrapper.attributes('href')).toBe('/users/new')
    expect(wrapper.attributes('type')).toBeUndefined()
  })

  it('blocks interaction while loading and announces it', () => {
    const wrapper = mount(CalButton, { props: { loading: true }, slots: { default: 'Installieren' } })

    expect(wrapper.attributes('disabled')).toBeDefined()
    expect(wrapper.attributes('aria-busy')).toBe('true')
    expect(wrapper.find('.cal-btn__spinner').exists()).toBe(true)
  })

  it('shows the icon only while idle so it does not compete with the spinner', async () => {
    const wrapper = mount(CalButton, { props: { icon: Plus }, slots: { default: 'Neu' } })
    expect(wrapper.find('.cal-icon').exists()).toBe(true)

    await wrapper.setProps({ loading: true })

    expect(wrapper.findAll('.cal-icon')).toHaveLength(0)
  })

  it('omits the label when the button is icon-only', () => {
    const wrapper = mount(CalButton, {
      props: { icon: Plus, iconOnly: true },
      slots: { default: 'Verborgen' },
    })

    expect(wrapper.find('.cal-btn__label').exists()).toBe(false)
  })

  it('emits click when enabled and stays silent when disabled', async () => {
    const wrapper = mount(CalButton, { slots: { default: 'Los' } })
    await wrapper.trigger('click')
    expect(wrapper.emitted('click')).toHaveLength(1)

    await wrapper.setProps({ disabled: true })
    await wrapper.trigger('click')

    expect(wrapper.emitted('click')).toHaveLength(1)
  })

  it('applies variant and size as classes', () => {
    const wrapper = mount(CalButton, { props: { variant: 'danger', size: 'sm' } })

    expect(wrapper.classes()).toContain('is-danger')
    expect(wrapper.classes()).toContain('is-sm')
  })

  it('resolves the real RouterLink component when routing is available', () => {
    const wrapper = mount(CalButton, {
      props: { to: '/plugins' },
      global: { stubs: { RouterLink: true } },
    })

    expect(wrapper.findComponent(RouterLink).exists()).toBe(true)
  })
})
