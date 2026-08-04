import { computed, ref, type ComputedRef } from 'vue'
import type { ConfirmRequest } from './confirmRequest'

/**
 * Asks the operator to confirm a consequential action.
 *
 * Replaces `window.confirm`, which could not be styled, could not explain the
 * consequence beyond one line, and blocks the browser's main thread. The API
 * stays await-shaped, so a call site reads exactly as before:
 *
 *   if (!(await confirm({ title: 'Benutzer löschen?' }))) return
 *
 * The dialog itself is rendered once by CalConfirmHost in the app shell.
 */
interface PendingConfirm {
  readonly request: ConfirmRequest
  readonly settle: (confirmed: boolean) => void
}

// A queue rather than a single slot: two overlapping asks must not silently
// drop one another — the second waits and is answered afterwards.
const queue = ref<PendingConfirm[]>([])

export function confirm(request: ConfirmRequest): Promise<boolean> {
  return new Promise<boolean>((resolve) => {
    queue.value = [...queue.value, { request, settle: resolve }]
  })
}

export function useConfirmDialog(): {
  current: ComputedRef<ConfirmRequest | null>
  answer: (confirmed: boolean) => void
} {
  const current = computed(() => queue.value[0]?.request ?? null)

  function answer(confirmed: boolean): void {
    const pending = queue.value[0]
    if (!pending) {
      return
    }
    queue.value = queue.value.slice(1)
    pending.settle(confirmed)
  }

  return { current, answer }
}

/** Rejects every open request as "cancelled" — for tests, and on route teardown. */
export function resetConfirm(): void {
  for (const pending of queue.value) {
    pending.settle(false)
  }
  queue.value = []
}
