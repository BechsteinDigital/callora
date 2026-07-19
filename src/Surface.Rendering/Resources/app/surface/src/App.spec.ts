import { mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { describe, it, expect } from 'vitest'
import App from './App.vue'
import { createSurfaceRegistry } from './surface-registry'

const context = { workspaceKey: 'acme', surfaceKey: 'portal' }

describe('surface host (grundgerüst)', () => {
  it('shows a neutral empty state when no plugin registered a view', () => {
    const registry = createSurfaceRegistry()

    const wrapper = mount(App, { props: { context, registry } })

    expect(wrapper.get('[data-testid="surface-empty"]').text()).toContain('Keine Oberfläche')
  })

  it('renders a plugin-registered view and passes it the surface context', () => {
    const registry = createSurfaceRegistry()
    registry.registerView({
      id: 'demo',
      component: defineComponent({
        props: { context: { type: Object, required: true } },
        setup: (props) => () =>
          h('span', { 'data-testid': 'demo' }, (props.context as typeof context).workspaceKey),
      }),
    })

    const wrapper = mount(App, { props: { context, registry } })

    expect(wrapper.find('[data-testid="surface-empty"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="demo"]').text()).toBe('acme')
  })
})
