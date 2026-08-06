import type {
  SurfaceContextChannel,
  SurfaceContextDescriptor,
  SurfaceContextPublisher,
} from '../surface-context-channel'

/**
 * The context channel islands collaborate over, or undefined when the runtime has not
 * initialised. Returned rather than thrown, for the same reason a missing registry warns
 * instead of crashing: a plugin must never break the shell it is a guest in.
 */
export function surfaceContextChannel(): SurfaceContextChannel | undefined {
  const channel = window.calloraSurface?.contextChannel
  if (!channel) {
    console.warn('[callora-surface] surface runtime not initialised; no context channel.')
  }

  return channel
}

/** Everything one view took from the channel, so a single call gives it all back. */
export interface SurfaceContextScope {
  /** Publishes under a key this scope owns until it is disposed. */
  publish<T = unknown>(descriptor: SurfaceContextDescriptor): SurfaceContextPublisher<T>
  /** Subscribes for as long as this scope lives. */
  subscribe<T = unknown>(key: string, handler: (value: T | undefined) => void): void
  /** Releases every publisher and subscription taken through this scope. */
  dispose(): void
}

/**
 * A scope for one view's use of the channel. Call it in `setup()` and hand `dispose` to
 * `onUnmounted`: a view that leaves the page must not keep a key claimed or keep
 * receiving values into a component that no longer exists.
 */
export function createSurfaceContextScope(): SurfaceContextScope {
  const channel = surfaceContextChannel()
  const publishers: SurfaceContextPublisher[] = []
  const unsubscribes: (() => void)[] = []

  return {
    publish<T>(descriptor: SurfaceContextDescriptor): SurfaceContextPublisher<T> {
      // Without a channel the scope still hands back a publisher — an inert one. A view
      // that publishes on every keystroke should not need a null check per call.
      const publisher = channel?.providePublisher<T>(descriptor) ?? {
        accepted: false,
        publish: () => {},
        clear: () => {},
        dispose: () => {},
      }
      publishers.push(publisher as SurfaceContextPublisher)
      return publisher
    },
    subscribe<T>(key: string, handler: (value: T | undefined) => void): void {
      const unsubscribe = channel?.subscribe<T>(key, handler)
      if (unsubscribe) {
        unsubscribes.push(unsubscribe)
      }
    },
    dispose(): void {
      unsubscribes.splice(0).forEach((unsubscribe) => unsubscribe())
      publishers.splice(0).forEach((publisher) => publisher.dispose())
    },
  }
}
