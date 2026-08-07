import { onUnmounted, ref, type Ref } from 'vue'
import { createSurfaceContextScope } from '@callora/surface'
import type { SurfaceCallView } from './context-keys'

/**
 * Subscribes a block to one call context key for as long as it is on the page.
 *
 * A block declares what it needs and does not fetch it: whether the value came from an island
 * in the same tab or from the server is the resolver's business, and the block is the same
 * either way. The scope is released on unmount — a panel that left the page must not keep
 * receiving values into a component that no longer exists.
 */
export function useCallContext(key: string): Ref<SurfaceCallView | null> {
  const call = ref<SurfaceCallView | null>(null)
  const scope = createSurfaceContextScope()

  scope.subscribe<SurfaceCallView>(key, (value) => {
    call.value = value ?? null
  })

  onUnmounted(() => scope.dispose())

  return call
}

/** Seconds elapsed since an ISO instant, or null when it is not a time. */
export function secondsSince(iso: string | undefined, now: number): number | null {
  if (!iso) {
    return null
  }

  const started = new Date(iso).getTime()
  return Number.isNaN(started) ? null : Math.max(0, Math.floor((now - started) / 1000))
}

/** mm:ss, because a call is read in minutes and nobody counts to 3600. */
export function formatDuration(seconds: number | null): string {
  if (seconds === null) {
    return '–'
  }

  const minutes = Math.floor(seconds / 60)
  const rest = seconds % 60
  return `${minutes}:${String(rest).padStart(2, '0')}`
}
