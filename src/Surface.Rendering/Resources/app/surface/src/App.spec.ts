import { mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { describe, it, expect } from 'vitest'
import App from './App.vue'
import { createSurfaceRegistry } from './surface-registry'

const guestCaller = {
  state: 'guest' as const,
  subject: { issuer: 'callora.surface-guest', subjectId: '' },
}

const context = { workspaceKey: 'acme', surfaceKey: 'portal', caller: guestCaller }

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

  it('renders a surface-scoped view only on its assigned surface', () => {
    const registry = createSurfaceRegistry()
    registry.registerView({
      id: 'videoconference.room',
      component: defineComponent({ render: () => h('span', { 'data-testid': 'room' }) }),
      surfaceKeys: ['videoconference'],
    })

    const otherSurface = mount(App, { props: { context, registry } })
    const conferenceSurface = mount(App, {
      props: {
        context: { ...context, surfaceKey: 'videoconference' },
        registry,
      },
    })

    expect(otherSurface.find('[data-testid="room"]').exists()).toBe(false)
    expect(otherSurface.find('[data-testid="surface-empty"]').exists()).toBe(true)
    expect(conferenceSurface.find('[data-testid="room"]').exists()).toBe(true)
  })
})
