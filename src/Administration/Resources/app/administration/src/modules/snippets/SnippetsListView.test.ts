import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { ref } from 'vue'
import SnippetsListView from './SnippetsListView.vue'
import type { Snippet } from './snippetsApi'

const { listMock, setMock, resetMock } = vi.hoisted(() => ({
  listMock: vi.fn(),
  setMock: vi.fn(),
  resetMock: vi.fn(),
}))

vi.mock('./snippetsApi', () => ({
  snippetsApi: { list: listMock, set: setMock, reset: resetMock },
}))

const contextRef = ref<{ userId: string; permissions: string[] } | null>(null)
vi.mock('@/core/auth/authStore', () => ({ useAuthStore: () => ({ context: contextRef }) }))

function snippet(overrides: Partial<Snippet> = {}): Snippet {
  return {
    snippetKey: 'shop.cart.title',
    locale: 'de',
    pluginId: 'shop',
    baseValue: 'Warenkorb',
    overrideValue: null,
    effectiveValue: 'Warenkorb',
    isOverridden: false,
    isOrphaned: false,
    ...overrides,
  }
}

beforeEach(() => {
  listMock.mockReset().mockResolvedValue([snippet()])
  setMock.mockReset().mockResolvedValue(undefined)
  resetMock.mockReset().mockResolvedValue(undefined)
  contextRef.value = { userId: 'root', permissions: ['snippet.read', 'snippet.update'] }
})

describe('Textverwaltung', () => {
  // Gezeigt wird EINE Ebene, nie die aufgelöste Kette: Sonst wäre nicht zu erkennen, was das
  // Zurücknehmen einer Zeile bewirkt.
  it('fragt genau die Ebene ab, die eingestellt ist', async () => {
    mount(SnippetsListView)
    await flushPromises()

    expect(listMock).toHaveBeenCalledWith({ locale: 'de', scope: 'global', scopeKey: '' })
  })

  it('zeigt den Text des Pakets neben dem, was hier gilt', async () => {
    listMock.mockResolvedValue([snippet({ overrideValue: 'Bestellung', effectiveValue: 'Bestellung', isOverridden: true })])

    const wrapper = mount(SnippetsListView)
    await flushPromises()

    expect(wrapper.text()).toContain('Warenkorb')
    expect(wrapper.find('input[name="value-shop.cart.title"]').attributes('value')).toBe('Bestellung')
  })

  // Ein Paket, das seinen Schlüssel aufgibt, macht die Arbeit des Betreibers sonst unsichtbar.
  it('markiert eine verwaiste Abweichung, statt sie zu verstecken', async () => {
    listMock.mockResolvedValue([
      snippet({ baseValue: null, overrideValue: 'Bleibt', effectiveValue: 'Bleibt', isOverridden: true, isOrphaned: true }),
    ])

    const wrapper = mount(SnippetsListView)
    await flushPromises()

    expect(wrapper.text()).toContain('verwaist')
  })

  it('zeigt kein Zurücksetzen, wo nichts gesetzt ist', async () => {
    const wrapper = mount(SnippetsListView)
    await flushPromises()

    expect(wrapper.findAll('button').map((button) => button.text())).not.toContain('Zurücksetzen')
  })

  it('lässt ohne das Recht zum Ändern nur lesen', async () => {
    contextRef.value = { userId: 'leser', permissions: ['snippet.read'] }

    const wrapper = mount(SnippetsListView)
    await flushPromises()

    expect(wrapper.findAll('button').map((button) => button.text())).not.toContain('Speichern')
  })

  it('schickt den geänderten Text an die Ebene, die eingestellt ist', async () => {
    const wrapper = mount(SnippetsListView)
    await flushPromises()

    await wrapper.get('input[name="value-shop.cart.title"]').setValue('Bestellung')
    await wrapper.findAll('button').find((button) => button.text() === 'Speichern')!.trigger('click')
    await flushPromises()

    expect(setMock).toHaveBeenCalledWith(
      'shop.cart.title',
      { locale: 'de', scope: 'global', scopeKey: '' },
      'Bestellung',
    )
  })
})
