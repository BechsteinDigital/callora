import { describe, it, expect, vi, afterEach } from 'vitest'
import { defineComponent } from 'vue'
import {
  registerBlock,
  registerBlockCategory,
  registerControlType,
  registerSurfaceView,
  type BlockRegistry,
  type SurfaceRegistry,
} from './index'

const stub = defineComponent({ render: () => null })

/**
 * Nur so viel Registry, wie die geprüfte Funktion anfasst. Über `unknown` gecastet statt
 * das Interface zu erfüllen: ein vollständiges Mock würde bei jeder Vertragserweiterung
 * mitwachsen müssen, ohne dafür etwas zu prüfen.
 */
type PartialRegistry = Partial<Omit<SurfaceRegistry, 'blocks'>> & {
  blocks?: Partial<BlockRegistry>
}

function fakeRegistry(overrides: PartialRegistry) {
  window.calloraSurface = { views: [], ...overrides } as unknown as SurfaceRegistry
}

afterEach(() => {
  delete window.calloraSurface
  vi.restoreAllMocks()
})

describe('registerSurfaceView', () => {
  it('forwards the view to the runtime registry when it is present', () => {
    const registerView = vi.fn()
    fakeRegistry({ registerView })

    registerSurfaceView({ id: 'voip.calls', component: stub })

    expect(registerView).toHaveBeenCalledWith({ id: 'voip.calls', component: stub })
  })

  it('forwards a surface allowlist as part of the public view contract', () => {
    const registerView = vi.fn()
    fakeRegistry({ registerView })

    registerSurfaceView({
      id: 'videoconference.room',
      component: stub,
      surfaceKeys: ['videoconference'],
    })

    expect(registerView).toHaveBeenCalledWith({
      id: 'videoconference.room',
      component: stub,
      surfaceKeys: ['videoconference'],
    })
  })

  it('is a no-op with a warning (never throws) when the runtime is absent', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})

    expect(() => registerSurfaceView({ id: 'voip.calls', component: stub })).not.toThrow()
    expect(warn).toHaveBeenCalledOnce()
    expect(warn.mock.calls[0]?.[0]).toContain('voip.calls')
  })
})

describe('blocks', () => {
  it('forwards a block to the runtime block registry', () => {
    const register = vi.fn()
    fakeRegistry({ blocks: { registerBlock: register } })

    registerBlock({
      id: 'communication.call-list',
      label: 'Anrufliste',
      category: 'telephony',
      requires: ['communication.active-call/v1'],
      component: stub,
      controls: {
        title: { type: 'text', label: 'Überschrift' },
        accent: { type: 'colorToken', label: 'Akzent' },
      },
    })

    expect(register).toHaveBeenCalledOnce()
    expect(register.mock.calls[0]?.[0]).toMatchObject({ id: 'communication.call-list' })
  })

  it('forwards a category', () => {
    const register = vi.fn()
    fakeRegistry({ blocks: { registerBlockCategory: register } })

    registerBlockCategory({ id: 'telephony', label: 'Telefonie', icon: 'phone' })

    expect(register).toHaveBeenCalledWith({ id: 'telephony', label: 'Telefonie', icon: 'phone' })
  })

  it('forwards a contributed control type', () => {
    const register = vi.fn()
    fakeRegistry({ blocks: { registerControlType: register } })

    registerControlType('communication.phoneNumber')

    expect(register).toHaveBeenCalledWith('communication.phoneNumber')
  })

  it.each([
    ['registerBlock', () => registerBlock({ id: 'x', label: 'X', category: 'c', component: stub })],
    ['registerBlockCategory', () => registerBlockCategory({ id: 'c', label: 'C' })],
    ['registerControlType', () => registerControlType('t')],
  ])('%s is a no-op with a warning when the runtime is absent', (_name, call) => {
    // Ein Plugin darf die Schale, in der es zu Gast ist, nie brechen — auch nicht,
    // wenn es vor der Runtime lädt.
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})

    expect(call).not.toThrow()
    expect(warn).toHaveBeenCalledOnce()
  })
})
