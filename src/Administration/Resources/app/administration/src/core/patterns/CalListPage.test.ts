import { afterEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import CalListPage from './CalListPage.vue'
import { registerExtension, resetExtensions } from '@/core/extensions/registry'

const Marker = { setup: () => () => h('span', { class: 'marker' }, 'x') }
const routerStubs = { RouterLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

type PageProps = InstanceType<typeof CalListPage>['$props']

function mountPage(props: PageProps, slots: Record<string, string> = {}) {
  return mount(CalListPage, { props, slots, global: { stubs: routerStubs } })
}

describe('CalListPage', () => {
  afterEach(resetExtensions)

  it('renders its title and description', () => {
    const wrapper = mountPage({ module: 'users', title: 'Benutzer', description: 'Wer Zugang hat.' })

    expect(wrapper.text()).toContain('Benutzer')
    expect(wrapper.text()).toContain('Wer Zugang hat.')
  })

  it('renders the default slot as the page body', () => {
    const wrapper = mountPage({ module: 'users', title: 'Benutzer' }, { default: '<p class="body">Tabelle</p>' })

    expect(wrapper.find('.body').exists()).toBe(true)
  })

  it('brings its toolbar extension slot, derived from the module name', () => {
    registerExtension('users.list.toolbar', Marker)

    const wrapper = mountPage({ module: 'users', title: 'Benutzer' })

    expect(wrapper.find('.marker').exists()).toBe(true)
  })

  it('derives the slot name from whatever module it is given', () => {
    registerExtension('webhooks.list.toolbar', Marker)

    // A contribution to another module's toolbar must not leak into this one.
    expect(mountPage({ module: 'webhooks', title: 'Webhooks' }).find('.marker').exists()).toBe(true)
    expect(mountPage({ module: 'users', title: 'Benutzer' }).find('.marker').exists()).toBe(false)
  })

  it('renders the view\'s own actions alongside the extension slot', () => {
    registerExtension('users.list.toolbar', Marker)

    const wrapper = mountPage({ module: 'users', title: 'Benutzer' }, { actions: '<button class="new">Neu</button>' })

    expect(wrapper.find('.new').exists()).toBe(true)
    expect(wrapper.find('.marker').exists()).toBe(true)
  })

  it('hands the context to the extension slot, so a contribution can scope itself', () => {
    const seen: unknown[] = []
    registerExtension(
      'users.list.toolbar',
      defineComponent({
        props: { ctx: { type: null, required: false } },
        setup(props) {
          seen.push(props.ctx)
          return () => h('span')
        },
      }),
    )

    mountPage({ module: 'users', title: 'Benutzer', ctx: { workspaceKey: 'acme' } })

    expect(seen).toEqual([{ workspaceKey: 'acme' }])
  })

  it('renders a back link when given one', () => {
    const wrapper = mountPage({ module: 'users', title: 'Benutzer', backTo: '/users' })

    expect(wrapper.find('a[href="/users"]').exists()).toBe(true)
  })

  it('renders fine when nothing contributed to its toolbar', () => {
    const wrapper = mountPage({ module: 'users', title: 'Benutzer' })

    expect(wrapper.text()).toContain('Benutzer')
    expect(wrapper.find('.marker').exists()).toBe(false)
  })
})
