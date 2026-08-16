import { readFileSync } from 'node:fs'
import { describe, it, expect, beforeEach } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import { createRouter, createMemoryHistory, useRoute } from 'vue-router'
import { mount, flushPromises } from '@vue/test-utils'
import AppRouterView from './AppRouterView.vue'

// Zwei Pfade, eine Komponente — der Zuschnitt, den users/new + users/:userId, roles/new +
// roles/:role und workspaces/new + workspaces/:workspaceKey teilen. Die Ansicht liest ihren
// Parameter EINMAL beim Mount, wie es die drei Detailformulare mit `onMounted(load)` tun.
let mounts = 0

const Detail = defineComponent({
  setup() {
    mounts += 1
    const route = useRoute()
    const externalId = ref(String(route.params.userId ?? ''))
    return () => h('div', { class: 'detail' }, externalId.value || '(leer)')
  },
})

// Der Gegenfall: eine Ansicht, die den Parameterwechsel selbst verarbeitet und ihren
// Zustand dabei behalten soll (der Flächenbaum). Sie trägt auf allen ihren Pfaden
// denselben `meta.viewKey`.
const Tree = defineComponent({
  setup() {
    mounts += 1
    const expanded = ref('aufgeklappt')
    return () => h('div', { class: 'tree' }, expanded.value)
  },
})

function makeRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/users/new', component: Detail },
      { path: '/users/:userId', component: Detail },
      { path: '/surfaces', component: Tree, meta: { viewKey: 'surfaces' } },
      { path: '/surfaces/:surfaceKey', component: Tree, meta: { viewKey: 'surfaces' } },
    ],
  })
}

beforeEach(() => {
  mounts = 0
})

describe('AppRouterView', () => {
  it('verwirft die Instanz beim Wechsel von einem Datensatz auf einen anderen', async () => {
    const router = makeRouter()
    await router.push('/users/alice')
    const wrapper = mount(AppRouterView, { global: { plugins: [router] } })
    await flushPromises()

    await router.push('/users/bob')
    await flushPromises()

    expect(mounts).toBe(2)
    expect(wrapper.get('.detail').text()).toBe('bob')
  })

  it('verwirft die Instanz beim Wechsel von einem Datensatz auf das leere Formular', async () => {
    const router = makeRouter()
    await router.push('/users/alice')
    const wrapper = mount(AppRouterView, { global: { plugins: [router] } })
    await flushPromises()

    await router.push('/users/new')
    await flushPromises()

    // Ohne den Key steht hier „alice“ — und ein Speichern auf /users/new legt sie erneut an.
    expect(wrapper.get('.detail').text()).toBe('(leer)')
  })

  it('behält die Instanz, wenn die Ansicht den Parameterwechsel selbst verarbeitet', async () => {
    const router = makeRouter()
    await router.push('/surfaces')
    mount(AppRouterView, { global: { plugins: [router] } })
    await flushPromises()

    await router.push('/surfaces/impressum')
    await flushPromises()

    expect(mounts).toBe(1)
  })

  // Der Fix wirkt nur, solange die Shell die Ansicht durch diese Komponente rendert. Ein
  // direktes <RouterView> dort bringt den Befund aus #292 zurück, ohne dass ein Test der
  // Ansichten selbst etwas merkt — deshalb wird die Stelle festgehalten, nicht nur das
  // Verhalten dahinter.
  it('wird von der Shell benutzt, statt dass diese die Router-View selbst rendert', () => {
    // process.cwd() wie in den anderen Datei-lesenden Tests: import.meta.url ist unter
    // Vitest keine file:-URL, readFileSync kann sie deshalb nicht nehmen.
    const shell = readFileSync(`${process.cwd()}/src/app/AppShell.vue`, 'utf8')

    expect(shell).toContain('<AppRouterView />')
    expect(shell).not.toMatch(/<RouterView[\s>]/)
  })
})
