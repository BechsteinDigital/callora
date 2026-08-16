import { ref, type Ref } from 'vue'
import type { ToastTone } from './toast'

/**
 * Transient feedback about something that just happened ("Plugin aktiviert",
 * "Speichern fehlgeschlagen"). It is deliberately separate from CalAlert: an
 * alert describes the state of the content in front of you, a toast reports the
 * outcome of an action and then disappears.
 */
export interface Toast {
  readonly id: number
  readonly tone: ToastTone
  readonly message: string
  readonly description?: string
}

// Module singleton, like the other stores: any module can report an outcome
// without the view it lives in having to own a notification area.
const toasts = ref<Toast[]>([])
const timers = new Map<number, ReturnType<typeof setTimeout>>()
let nextId = 1

/** How long each tone stays on screen. Failures linger — they carry more text. */
const LIFETIME_MS: Record<ToastTone, number> = {
  success: 4000,
  info: 5000,
  warning: 7000,
  danger: 9000,
}

function dismiss(id: number): void {
  const timer = timers.get(id)
  if (timer) {
    clearTimeout(timer)
    timers.delete(id)
  }
  toasts.value = toasts.value.filter((toast) => toast.id !== id)
}

function push(tone: ToastTone, message: string, description?: string): number {
  const id = nextId++
  toasts.value = [...toasts.value, { id, tone, message, description }]
  timers.set(
    id,
    setTimeout(() => dismiss(id), LIFETIME_MS[tone]),
  )
  return id
}

export function useToasts(): {
  toasts: Ref<Toast[]>
  dismiss: (id: number) => void
} {
  return { toasts, dismiss }
}

/**
 * The reporting surface used from anywhere — stores, API layers, views. Kept as
 * plain functions rather than a composable so a non-component module can call it.
 */
export const toast = {
  success: (message: string, description?: string) => push('success', message, description),
  info: (message: string, description?: string) => push('info', message, description),
  warning: (message: string, description?: string) => push('warning', message, description),
  /** Accepts an Error directly — the common case in a catch block. */
  error: (error: unknown, description?: string) =>
    push('danger', error instanceof Error ? error.message : String(error), description),
}

/** Clears every toast and its timer — on logout (via `endSession`), and for tests. */
export function resetToasts(): void {
  for (const timer of timers.values()) {
    clearTimeout(timer)
  }
  timers.clear()
  toasts.value = []
  nextId = 1
}
