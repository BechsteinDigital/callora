import { describe, it, expect, beforeEach, vi } from 'vitest'
import { reportClientError, resetClientErrorReporting } from './clientErrorReporting'

function fetchMock() {
  const mock = vi.fn().mockResolvedValue(new Response(null, { status: 202 }))
  globalThis.fetch = mock as unknown as typeof fetch
  return mock
}

function bodyOf(mock: ReturnType<typeof fetchMock>, call = 0): Record<string, unknown> {
  return JSON.parse((mock.mock.calls[call][1] as RequestInit).body as string)
}

beforeEach(() => {
  resetClientErrorReporting()
})

describe('reportClientError', () => {
  it('schickt Meldung, Stack und Pfad an die Senke des Hosts', () => {
    const mock = fetchMock()

    reportClientError(new Error('Cannot read properties of undefined'), '/users/alice')

    expect(mock).toHaveBeenCalledOnce()
    expect(mock.mock.calls[0][0]).toBe('/api/client-errors')
    expect(bodyOf(mock)).toMatchObject({
      source: 'admin',
      message: 'Cannot read properties of undefined',
      url: '/users/alice',
    })
    expect(bodyOf(mock).stack).toBeTruthy()
  })

  // Ohne das dreht sich eine Seite ohne Netz in ihrer eigenen Fehlerbehandlung.
  it('schluckt einen Fehlschlag der Meldung, statt ihn zu einem Fehler zu machen', async () => {
    const mock = vi.fn().mockRejectedValue(new Error('offline'))
    globalThis.fetch = mock as unknown as typeof fetch

    expect(() => reportClientError(new Error('boom'))).not.toThrow()
    await Promise.resolve()
    expect(mock).toHaveBeenCalledOnce()
  })

  // Ein Fehler im Render-Zyklus tritt pro Frame auf. Wer jeden Frame meldet, füllt das Logziel
  // mit derselben Zeile.
  it('meldet denselben Fehler nur einmal', () => {
    const mock = fetchMock()
    const error = new Error('immer derselbe')

    reportClientError(error)
    reportClientError(error)
    reportClientError(error)

    expect(mock).toHaveBeenCalledOnce()
  })

  it('hört nach der Obergrenze eines Seitenlebens auf', () => {
    const mock = fetchMock()

    for (let i = 0; i < 25; i++) {
      reportClientError(new Error(`fehler ${i}`))
    }

    expect(mock).toHaveBeenCalledTimes(10)
  })

  it('nimmt auch etwas entgegen, das kein Error ist', () => {
    const mock = fetchMock()

    reportClientError('einfach nur ein String')

    expect(bodyOf(mock)).toMatchObject({ message: 'einfach nur ein String' })
    expect(bodyOf(mock).stack).toBeUndefined()
  })
})
