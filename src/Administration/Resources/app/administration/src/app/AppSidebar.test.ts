import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import AppSidebar from './AppSidebar.vue'
import type { AdminContext } from '@/core/auth/adminContext'
import { usePluginNavigation } from '@/core/extensions/pluginNavigation'
import { resetAreaContext, useAreaContext } from './areaContext'
import { resetSidebar, useSidebar } from './sidebarState'

const { contextRef, currentRoute } = vi.hoisted(() => ({
  contextRef: { value: null as AdminContext | null },
  // The component reads route.path, so the stub must expose that shape.
  currentRoute: { path: '/' },
}))

vi.mock('@/core/auth/authStore', () => ({ useAuthStore: () => ({ context: contextRef }) }))
// A real ref, not a plain object: the template unwraps it, and a plain stand-in
// would leave `pluginNav.length` undefined.
vi.mock('@/core/extensions/pluginNavigation', async () => {
  const { ref } = await import('vue')
  const items = ref<unknown[]>([])
  return { usePluginNavigation: () => ({ items }) }
})
vi.mock('vue-router', () => ({
  useRoute: () => currentRoute,
  RouterLink: { props: ['to'], template: '<a :href="to" :class="$attrs.class"><slot /></a>' },
}))

function ctx(permissions: string[]): AdminContext {
  return {
    userId: 'u',
    displayName: null,
    email: null,
    roles: [],
    permissions,
    scope: null,
    workspaceKey: null,
    tenantKey: null,
    isOperator: true,
  }
}

beforeEach(() => {
  contextRef.value = ctx(['*'])
  usePluginNavigation().items.value = []
  currentRoute.path = '/'
  localStorage.clear()
  resetSidebar()
  resetAreaContext()
})

describe('AppSidebar', () => {
  it('shows the platform area to an operator, not everything at once', () => {
    // Der Punkt der Bereiche: Wer die Instanz betreibt, soll nicht an Medien und Flows
    // vorbeiscrollen, um zu den Mandanten zu kommen. „Inhalte" gehört dem Workspace und
    // steht deshalb erst da, wenn man dorthin wechselt.
    const wrapper = mount(AppSidebar)

    const headings = wrapper.findAll('.sidebar__group-label').map((n) => n.text())
    expect(headings).toEqual(['Verwaltung', 'System'])
  })

  it('shows the workspace area once the operator switches to it', () => {
    useAreaContext().setActive('workspace')

    const wrapper = mount(AppSidebar)

    const headings = wrapper.findAll('.sidebar__group-label').map((n) => n.text())
    expect(headings).toContain('Inhalte')
    expect(headings).not.toContain('Verwaltung')
  })

  it('omits a heading for a group whose items are all gated away', () => {
    contextRef.value = ctx(['user.read'])

    const wrapper = mount(AppSidebar)

    expect(wrapper.findAll('.sidebar__group-label').map((n) => n.text())).toEqual(['Verwaltung'])
  })

  it('marks exactly the section the current route belongs to', () => {
    currentRoute.path = '/users/u-1'

    const wrapper = mount(AppSidebar)

    const active = wrapper.findAll('.sidebar__link.is-active')
    expect(active).toHaveLength(1)
    expect(active[0].text()).toBe('Benutzer')
  })

  it('does not leave the dashboard lit on a sub-route', () => {
    currentRoute.path = '/plugins'

    const wrapper = mount(AppSidebar)

    expect(wrapper.findAll('.sidebar__link.is-active').map((n) => n.text())).toEqual(['Plugins'])
  })

  it('appends plugin-contributed entries under their own heading', () => {
    usePluginNavigation().items.value = [
      {
        pluginId: 'communication',
        id: 'main',
        label: 'Telefonie',
        to: '/extensions/communication',
        icon: 'phone',
        order: 10,
      },
    ]

    const wrapper = mount(AppSidebar)

    expect(wrapper.findAll('.sidebar__group-label').map((n) => n.text())).toContain('Erweiterungen')
    expect(wrapper.text()).toContain('Telefonie')
  })

  it('hides labels and the wordmark when collapsed, keeping the links reachable', async () => {
    const wrapper = mount(AppSidebar)
    expect(wrapper.find('.sidebar__wordmark').exists()).toBe(true)

    useSidebar().toggleCollapsed()
    await wrapper.vm.$nextTick()

    expect(wrapper.find('.sidebar__wordmark').exists()).toBe(false)
    expect(wrapper.findAll('.sidebar__link-label')).toHaveLength(0)
    expect(wrapper.findAll('.sidebar__link').length).toBeGreaterThan(0)
  })

  it('titles the links when collapsed so the icons stay identifiable', async () => {
    const wrapper = mount(AppSidebar)
    useSidebar().toggleCollapsed()
    await wrapper.vm.$nextTick()

    expect(wrapper.find('.sidebar__link').attributes('title')).toBe('Übersicht')
  })

  it('closes the mobile drawer when a destination is chosen', async () => {
    const wrapper = mount(AppSidebar)
    const sidebar = useSidebar()
    sidebar.openMobile()

    await wrapper.findAll('.sidebar__link')[1].trigger('click')

    expect(sidebar.mobileOpen.value).toBe(false)
  })
})
