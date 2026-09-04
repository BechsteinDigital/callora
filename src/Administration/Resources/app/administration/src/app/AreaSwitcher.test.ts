import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref } from 'vue'
import AreaSwitcher from './AreaSwitcher.vue'
import { resetAreaContext } from './areaContext'
import type { AdminContext } from '@/core/auth/adminContext'

const contextRef = ref<AdminContext | null>(null)
vi.mock('@/core/auth/authStore', () => ({ useAuthStore: () => ({ context: contextRef }) }))

function ctx(partial: Partial<AdminContext>): AdminContext {
  return {
    userId: 'u',
    displayName: null,
    email: null,
    roles: [],
    permissions: ['*'],
    scope: null,
    workspaceKey: null,
    tenantKey: null,
    isOperator: false,
    ...partial,
  }
}

beforeEach(() => {
  resetAreaContext()
  localStorage.clear()
})

describe('AreaSwitcher', () => {
  it('lets an operator choose, because they reach all three', () => {
    contextRef.value = ctx({ isOperator: true })

    const wrapper = mount(AreaSwitcher)

    expect(wrapper.find('select').exists()).toBe(true)
    expect(wrapper.findAll('option').map((o) => o.text())).toEqual([
      'Plattform',
      'Mandant',
      'Workspace',
    ])
  })

  it('shows a tenant session its area as text, with the tenant it is in', () => {
    // Ein Auswahlfeld mit genau einem Eintrag sieht aus wie eine Wahl und ist keine. Und
    // „Mandant" ohne den Namen sagt niemandem, wo er sitzt.
    contextRef.value = ctx({ scope: 'tenant', tenantKey: 'acme' })

    const wrapper = mount(AreaSwitcher)

    expect(wrapper.find('select').exists()).toBe(false)
    expect(wrapper.text()).toContain('Mandant')
    expect(wrapper.text()).toContain('acme')
  })

  it('shows a workspace session its workspace', () => {
    contextRef.value = ctx({ scope: 'workspace', workspaceKey: 'vertrieb' })

    const wrapper = mount(AreaSwitcher)

    expect(wrapper.find('select').exists()).toBe(false)
    expect(wrapper.text()).toContain('vertrieb')
  })

  it('renders nothing before sign-in', () => {
    contextRef.value = null

    expect(mount(AreaSwitcher).text()).toBe('')
  })

  it('drops a stored choice the session cannot reach', () => {
    // Eine Wahl aus einer früheren Sitzung überlebt die Anmeldung sonst als Bereich, den es
    // für diesen Menschen nicht gibt — und die Sidebar wäre leer, ohne dass etwas fehlschlägt.
    localStorage.setItem('callora.activeArea', 'platform')
    resetAreaContext()
    contextRef.value = ctx({ scope: 'workspace', workspaceKey: 'vertrieb' })

    expect(mount(AreaSwitcher).text()).toContain('Workspace')
  })
})
