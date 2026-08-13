import { describe, it, expect, beforeEach, vi } from 'vitest'
import { registerHook, runHook, resetHooks } from './hooks'

beforeEach(() => resetHooks())

describe('extension hooks', () => {
  it('is not canceled when no handlers are registered', async () => {
    const outcome = await runHook('users.before-save', { v: 1 })
    expect(outcome.canceled).toBe(false)
  })

  it('runs handlers in ascending order and lets them mutate the payload', async () => {
    const seen: number[] = []
    registerHook<{ v: number }>('h', (ctx) => {
      seen.push(2)
      ctx.payload.v += 1
    }, 10)
    registerHook<{ v: number }>('h', (ctx) => {
      seen.push(1)
      ctx.payload.v *= 2
    }, 1)

    const payload = { v: 3 }
    await runHook('h', payload)

    expect(seen).toEqual([1, 2]) // order 1 before order 10
    expect(payload.v).toBe(7) // (3*2) then +1 — mutations persist across handlers
  })

  it('cancels the action and short-circuits later handlers', async () => {
    const later = vi.fn()
    registerHook('h', (ctx) => ctx.cancel('nope'), 1)
    registerHook('h', later, 2)

    const outcome = await runHook('h', {})

    expect(outcome.canceled).toBe(true)
    expect(outcome.cancelReason).toBe('nope')
    expect(later).not.toHaveBeenCalled()
  })

  it('awaits async handlers', async () => {
    registerHook<{ done?: boolean }>('h', async (ctx) => {
      await Promise.resolve()
      ctx.payload.done = true
    })

    const payload: { done?: boolean } = {}
    await runHook('h', payload)

    expect(payload.done).toBe(true)
  })

  // #289: Ein werfender Handler riss vorher die Aufrufstelle mit. Bei "after" ist die Aktion
  // zu dem Zeitpunkt bereits gelungen — der Operator sah einen Fehlschlag fuer etwas, das
  // vollstaendig funktioniert hatte.
  it('lets a successful action stay successful when an after-handler throws', async () => {
    const seen: string[] = []
    registerHook('media.after-upload', () => {
      throw new Error('plugin ist kaputt')
    }, 0, 'acme-media')
    registerHook('media.after-upload', () => {
      seen.push('zweiter Handler')
    }, 1, 'other')

    const outcome = await runHook('media.after-upload', {})

    expect(outcome.canceled).toBe(false)
    expect(seen).toEqual(['zweiter Handler'])
    expect(outcome.failures).toHaveLength(1)
    expect(outcome.failures[0].pluginId).toBe('acme-media')
  })

  // Die andere Haelfte, und sie muss entgegengesetzt ausfallen: Ein before-Handler STEHT fuer
  // eine Pruefung. Wer ueber dessen Ausnahme hinweggeht, ueberspringt genau die Pruefung.
  it('treats a throwing before-handler as a cancel', async () => {
    const seen: string[] = []
    registerHook('users.before-save', () => {
      throw new Error('Berechtigungspruefung gescheitert')
    }, 0, 'acme-policy')
    registerHook('users.before-save', () => {
      seen.push('darf nicht laufen')
    }, 1, 'other')

    const outcome = await runHook('users.before-save', {})

    expect(outcome.canceled).toBe(true)
    expect(outcome.cancelReason).toContain('acme-policy')
    expect(seen).toEqual([])
  })

  // Ein Name, der in kein Schema passt, faellt auf die sichere Seite.
  it('fails closed for a hook name that is neither before nor after', async () => {
    registerHook('something.custom', () => {
      throw new Error('kaputt')
    }, 0, 'acme')

    const outcome = await runHook('something.custom', {})

    expect(outcome.canceled).toBe(true)
  })

  it('reports no failures when every handler succeeds', async () => {
    registerHook('media.after-upload', () => {}, 0, 'acme')

    const outcome = await runHook('media.after-upload', {})

    expect(outcome.failures).toEqual([])
  })
})
