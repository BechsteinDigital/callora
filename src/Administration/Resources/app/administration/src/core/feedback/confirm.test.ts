import { beforeEach, describe, expect, it } from 'vitest'
import { confirm, resetConfirm, useConfirmDialog } from './confirm'

describe('confirm dialog store', () => {
  beforeEach(() => {
    resetConfirm()
  })

  it('has nothing pending until something is asked', () => {
    expect(useConfirmDialog().current.value).toBeNull()
  })

  it('surfaces the request so the host can render it', () => {
    void confirm({ title: 'Benutzer löschen?', description: 'Das anonymisiert den Audit-Trail.' })

    expect(useConfirmDialog().current.value).toMatchObject({
      title: 'Benutzer löschen?',
      description: 'Das anonymisiert den Audit-Trail.',
    })
  })

  it('resolves true when the operator agrees', async () => {
    const answered = confirm({ title: 'Fortfahren?' })

    useConfirmDialog().answer(true)

    await expect(answered).resolves.toBe(true)
  })

  it('resolves false when the operator declines', async () => {
    const answered = confirm({ title: 'Fortfahren?' })

    useConfirmDialog().answer(false)

    await expect(answered).resolves.toBe(false)
  })

  it('clears the pending request once answered', () => {
    void confirm({ title: 'Fortfahren?' })
    const { answer, current } = useConfirmDialog()

    answer(true)

    expect(current.value).toBeNull()
  })

  it('answers overlapping requests one after another, in order', async () => {
    const first = confirm({ title: 'Erstes' })
    const second = confirm({ title: 'Zweites' })
    const { answer, current } = useConfirmDialog()

    expect(current.value?.title).toBe('Erstes')
    answer(true)

    expect(current.value?.title).toBe('Zweites')
    answer(false)

    await expect(first).resolves.toBe(true)
    await expect(second).resolves.toBe(false)
  })

  it('ignores an answer when nothing was asked', () => {
    expect(() => useConfirmDialog().answer(true)).not.toThrow()
  })

  it('treats a reset as a cancellation so no caller hangs', async () => {
    const answered = confirm({ title: 'Fortfahren?' })

    resetConfirm()

    await expect(answered).resolves.toBe(false)
  })
})
