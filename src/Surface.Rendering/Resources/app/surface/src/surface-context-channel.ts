/**
 * The channel islands collaborate over (#125 block C).
 *
 * `mountSurface` gives every island its own Vue app, and that isolation stays. What
 * two plugins need is not a shared app but a shared vocabulary: a CRM lead list
 * publishes `crm.lead-selection/v1`, a phone panel and a video block consume it, and
 * none of the three knows the others exist.
 *
 * Deliberately not an event bus. An untyped emitter accumulates undocumented events
 * nobody can enumerate, and nothing tells you who publishes what. Here every key is
 * namespaced and versioned, every publisher declares itself, and the channel can be
 * asked what is currently going on.
 *
 * It carries UI state, never authority. Anything that must be enforced goes through
 * an authorised API: a value on this channel arrived from another script on the same
 * page and proves nothing.
 */

/** Whether a key tolerates more than one publisher at a time. */
export type SurfaceContextCardinality = 'single' | 'multiple'

/** What a publisher declares before it may write to a key. */
export interface SurfaceContextDescriptor {
  /** Namespaced and versioned, for example `crm.lead-selection/v1`. */
  key: string
  /** Plugin claiming the key, so diagnostics can name it. */
  publisherPluginId: string
  /** Defaults to `single`: most workplace contexts have exactly one owner. */
  cardinality?: SurfaceContextCardinality
  /** Optional value check; a rejected value is not published and is counted. */
  validate?: (value: unknown) => boolean
}

/** A registered publisher's handle. Disposing it releases the key. */
export interface SurfaceContextPublisher<T = unknown> {
  /** Whether this publisher may write; false when the key is already owned. */
  readonly accepted: boolean
  publish(value: T): void
  clear(): void
  dispose(): void
}

/** What the channel is currently doing, for a diagnostics panel or a test. */
export interface SurfaceContextKeyDiagnostics {
  key: string
  publishers: readonly string[]
  subscriberCount: number
  hasValue: boolean
  rejectedPublishers: readonly string[]
  rejectedValues: number
}

export interface SurfaceContextChannel {
  readonly workspaceKey: string
  readonly surfaceKey: string
  providePublisher<T = unknown>(descriptor: SurfaceContextDescriptor): SurfaceContextPublisher<T>
  read<T = unknown>(key: string): T | undefined
  subscribe<T = unknown>(key: string, handler: (value: T | undefined) => void): () => void
  diagnostics(): readonly SurfaceContextKeyDiagnostics[]
}

interface KeyState {
  publishers: string[]
  rejectedPublishers: string[]
  rejectedValues: number
  subscribers: Set<(value: unknown) => void>
  value?: unknown
  hasValue: boolean
}

// Namespaced (at least one dot) and versioned (/vN). Both halves earn their place: the
// namespace keeps two plugins' keys apart, the version lets a publisher change shape
// without silently feeding an old consumer something it cannot read.
const KEY_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*(?:\.[a-z0-9]+(?:-[a-z0-9]+)*)+\/v\d+$/

/** Whether a key is namespaced and versioned. */
export function isSurfaceContextKey(key: string): boolean {
  return KEY_PATTERN.test(key)
}

export function createSurfaceContextChannel(
  workspaceKey: string,
  surfaceKey: string,
): SurfaceContextChannel {
  const keys = new Map<string, KeyState>()

  function state(key: string): KeyState {
    let existing = keys.get(key)
    if (!existing) {
      existing = {
        publishers: [],
        rejectedPublishers: [],
        rejectedValues: 0,
        subscribers: new Set(),
        hasValue: false,
      }
      keys.set(key, existing)
    }
    return existing
  }

  function emit(entry: KeyState): void {
    entry.subscribers.forEach((handler) => {
      try {
        handler(entry.value)
      } catch (error) {
        // One misbehaving consumer must not stop the others from being told.
        console.error('[callora-surface] a context subscriber threw', error)
      }
    })
  }

  return {
    workspaceKey,
    surfaceKey,

    providePublisher<T>(descriptor: SurfaceContextDescriptor): SurfaceContextPublisher<T> {
      if (!isSurfaceContextKey(descriptor.key)) {
        console.warn(
          `[callora-surface] "${descriptor.key}" is not a namespaced, versioned context key.`,
        )
        return rejectedPublisher<T>()
      }

      const entry = state(descriptor.key)
      const cardinality = descriptor.cardinality ?? 'single'
      if (cardinality === 'single' && entry.publishers.length > 0) {
        // Recorded rather than swallowed: two plugins claiming one context is a
        // composition mistake somebody has to be able to see.
        entry.rejectedPublishers.push(descriptor.publisherPluginId)
        console.warn(
          `[callora-surface] "${descriptor.key}" is already published by ` +
            `"${entry.publishers[0]}"; "${descriptor.publisherPluginId}" was refused.`,
        )
        return rejectedPublisher<T>()
      }

      entry.publishers.push(descriptor.publisherPluginId)
      let disposed = false

      return {
        accepted: true,
        publish(value: T): void {
          if (disposed) {
            return
          }
          if (descriptor.validate && !descriptor.validate(value)) {
            entry.rejectedValues += 1
            console.warn(`[callora-surface] a value for "${descriptor.key}" failed validation.`)
            return
          }
          entry.value = value
          entry.hasValue = true
          emit(entry)
        },
        clear(): void {
          if (disposed) {
            return
          }
          entry.value = undefined
          entry.hasValue = false
          emit(entry)
        },
        dispose(): void {
          if (disposed) {
            return
          }
          disposed = true
          const at = entry.publishers.indexOf(descriptor.publisherPluginId)
          if (at >= 0) {
            entry.publishers.splice(at, 1)
          }
          // The value belongs to its publisher: when the last one goes, consumers are
          // told the context is gone instead of holding a snapshot nobody maintains.
          if (entry.publishers.length === 0 && entry.hasValue) {
            entry.value = undefined
            entry.hasValue = false
            emit(entry)
          }
        },
      }
    },

    read<T>(key: string): T | undefined {
      return keys.get(key)?.value as T | undefined
    },

    subscribe<T>(key: string, handler: (value: T | undefined) => void): () => void {
      const entry = state(key)
      const wrapped = handler as (value: unknown) => void
      entry.subscribers.add(wrapped)

      // Late subscribers get the current snapshot immediately, so an island that
      // mounts after the publisher does not sit empty until the next change.
      if (entry.hasValue) {
        handler(entry.value as T)
      }

      return () => {
        entry.subscribers.delete(wrapped)
      }
    },

    diagnostics(): readonly SurfaceContextKeyDiagnostics[] {
      return [...keys.entries()].map(([key, entry]) => ({
        key,
        publishers: [...entry.publishers],
        subscriberCount: entry.subscribers.size,
        hasValue: entry.hasValue,
        rejectedPublishers: [...entry.rejectedPublishers],
        rejectedValues: entry.rejectedValues,
      }))
    },
  }
}

function rejectedPublisher<T>(): SurfaceContextPublisher<T> {
  return {
    accepted: false,
    publish(): void {},
    clear(): void {},
    dispose(): void {},
  }
}
