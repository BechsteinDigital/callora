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
})
