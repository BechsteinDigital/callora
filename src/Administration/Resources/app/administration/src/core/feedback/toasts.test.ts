import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { resetToasts, toast, useToasts } from './toasts'

describe('toast store', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    resetToasts()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('queues messages in the order they were reported', () => {
    toast.success('Plugin aktiviert')
    toast.info('Katalog aktualisiert')

    const { toasts } = useToasts()
    expect(toasts.value.map((t) => t.message)).toEqual(['Plugin aktiviert', 'Katalog aktualisiert'])
  })

  it('gives every toast its own identity', () => {
    const first = toast.success('A')
    const second = toast.success('A')

    expect(first).not.toBe(second)
  })

  it('takes the message from an Error, which is what a catch block holds', () => {
    toast.error(new Error('Verbindung verweigert'))

    expect(useToasts().toasts.value[0]).toMatchObject({ tone: 'danger', message: 'Verbindung verweigert' })
  })

  it('stringifies a non-Error rejection rather than dropping it', () => {
    toast.error('403')

    expect(useToasts().toasts.value[0].message).toBe('403')
  })

  it('retires a success message on its own', () => {
    toast.success('Gespeichert')

    vi.advanceTimersByTime(4000)

    expect(useToasts().toasts.value).toHaveLength(0)
  })

  it('keeps a failure on screen longer than a success', () => {
    toast.success('Gespeichert')
    toast.error(new Error('Fehlgeschlagen'))

    vi.advanceTimersByTime(4000)

    expect(useToasts().toasts.value.map((t) => t.message)).toEqual(['Fehlgeschlagen'])
  })

  it('dismisses on request and cancels the pending timer', () => {
    const id = toast.info('Hinweis')
    const { dismiss, toasts } = useToasts()

    dismiss(id)
    expect(toasts.value).toHaveLength(0)

    // The timer must not fire against an already-removed toast.
    expect(() => vi.advanceTimersByTime(10_000)).not.toThrow()
    expect(toasts.value).toHaveLength(0)
  })

  it('ignores a dismissal for an unknown id', () => {
    toast.info('Hinweis')

    useToasts().dismiss(999)

    expect(useToasts().toasts.value).toHaveLength(1)
  })
})
