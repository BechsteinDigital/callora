import { describe, it, expect, vi, afterEach } from 'vitest'
import { defineComponent } from 'vue'
import { registerSurfaceView, type SurfaceRegistry } from './index'

const stub = defineComponent({ render: () => null })

afterEach(() => {
  delete window.calloraSurface
  vi.restoreAllMocks()
})

describe('registerSurfaceView', () => {
  it('forwards the view to the runtime registry when it is present', () => {
    const registerView = vi.fn()
    window.calloraSurface = { views: [], registerView } as SurfaceRegistry

    registerSurfaceView({ id: 'voip.calls', component: stub })

    expect(registerView).toHaveBeenCalledWith({ id: 'voip.calls', component: stub })
  })

  it('is a no-op with a warning (never throws) when the runtime is absent', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})

    expect(() => registerSurfaceView({ id: 'voip.calls', component: stub })).not.toThrow()
    expect(warn).toHaveBeenCalledOnce()
    expect(warn.mock.calls[0]?.[0]).toContain('voip.calls')
  })
})
