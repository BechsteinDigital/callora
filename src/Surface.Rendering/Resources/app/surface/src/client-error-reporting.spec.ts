import { describe, it, expect, beforeEach, vi } from 'vitest'
import { reportClientError, resetClientErrorReporting } from './client-error-reporting'

function fetchMock() {
  const mock = vi.fn().mockResolvedValue(new Response(null, { status: 202 }))
  globalThis.fetch = mock as unknown as typeof fetch
  return mock
}

function bodyOf(mock: ReturnType<typeof fetchMock>): Record<string, unknown> {
  return JSON.parse((mock.mock.calls[0][1] as RequestInit).body as string)
}

beforeEach(() => {
  resetClientErrorReporting()
})

describe('client error reporting auf der Fläche', () => {
  it('meldet als surface und schickt nur den Pfad, nicht die Query', () => {
    const mock = fetchMock()
    window.history.replaceState({}, '', '/portal/termin?email=anna%40example.org')

    reportClientError(new Error('boom'))

    expect(mock.mock.calls[0][0]).toBe('/api/client-errors')
    expect(bodyOf(mock)).toMatchObject({ source: 'surface', url: '/portal/termin' })
    // Was in der Query einer Kundenseite steht, ist deren Sache — und verlässt das Gerät nicht.
    expect(JSON.stringify(bodyOf(mock))).not.toContain('anna')
  })

  it('schluckt einen Fehlschlag der Meldung', async () => {
    const mock = vi.fn().mockRejectedValue(new Error('offline'))
    globalThis.fetch = mock as unknown as typeof fetch

    expect(() => reportClientError(new Error('boom'))).not.toThrow()
    await Promise.resolve()
  })

  it('meldet denselben Fehler nur einmal', () => {
    const mock = fetchMock()
    const error = new Error('immer derselbe')

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
})
