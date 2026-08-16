import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { nextTick } from 'vue'
import { mountSurface } from './mount'
import { ensureSurfaceRegistry } from './public/bundles'
import { markBundlesSettled, resetBundleReadiness } from './bundle-readiness'
import { resetClientErrorReporting } from './client-error-reporting'

/**
 * Fällt ein Bundle aus (404, CSP, Netz), blieb beim Besucher ein dauerhaft leeres `div` ohne Text
 * und ohne Hinweis — die einzige Variante, die weder ihm noch dem Betrieb etwas sagt (#296).
 */
function island(viewId = 'communication.call-panel'): HTMLElement {
  document.body.replaceChildren()
  const element = document.createElement('div')
  element.dataset.calloraIsland = viewId
  element.dataset.workspace = 'acme'
  document.body.append(element)
  return element
}

beforeEach(() => {
  resetBundleReadiness()
  resetClientErrorReporting()
  globalThis.fetch = vi.fn().mockResolvedValue(new Response(null, { status: 202 })) as never
})

afterEach(() => {
  document.body.replaceChildren()
})

describe('eine Insel ohne Bundle', () => {
  it('bleibt leer, solange der Ladeversuch läuft', async () => {
    const element = island()

    mountSurface(ensureSurfaceRegistry('acme', 'agent-desk'))
    await nextTick()

    // Ein Platzhalter, der eine Sekunde später wieder verschwindet, ist schlimmer als nichts.
    expect(element.textContent).toBe('')
  })

  it('sagt dem Besucher, dass hier nichts kommt, sobald der Versuch vorbei ist', async () => {
    const element = island()

    mountSurface(ensureSurfaceRegistry('acme', 'agent-desk'))
    markBundlesSettled()
    await nextTick()

    expect(element.textContent).toContain('Dieser Bereich ist gerade nicht verfügbar.')
    expect(element.querySelector('.cal-island-unavailable')).not.toBeNull()
  })

  it('meldet den Ausfall an den Betrieb, und zwar einmal', async () => {
    island()

    mountSurface(ensureSurfaceRegistry('acme', 'agent-desk'))
    markBundlesSettled()
    await nextTick()
    await nextTick()

    const calls = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls
    const reports = calls.filter(([url]) => url === '/api/client-errors')
    expect(reports).toHaveLength(1)
    expect(JSON.parse((reports[0][1] as RequestInit).body as string).message).toContain(
      'communication.call-panel',
    )
  })
})
