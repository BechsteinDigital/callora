import type { Component } from 'vue'

/**
 * The Callora surface plugin contract. A surface plugin is a Vue bundle (built with
 * the {@link ./vite-preset calloraSurfacePlugin} preset — Vue external, resolved from
 * the runtime's window.CalloraVue) that registers views against the runtime. These
 * types mirror the surface runtime's registry; the runtime is the implementation, this
 * is the public contract plugin authors compile against.
 */

/** Read-only context the runtime hands each view (which workspace/surface renders). */
export interface SurfaceContext {
  workspaceKey: string
  surfaceKey: string
}

/** A view a plugin contributes — rendered as the whole app or into a matching island. */
export interface SurfaceView {
  /** Stable, unique id. Also the value of data-callora-island for island mounts. */
  id: string
  /** The Vue component; receives the SurfaceContext as a `context` prop. */
  component: Component
  /** Ascending render order in app mode; unset sorts as 0. */
  order?: number
  /**
   * Optional allowlist of surface keys this view belongs to. Omit it for a
   * workspace-wide contribution.
   */
  surfaceKeys?: readonly string[]
}

/** The runtime registry plugins dock into (window.calloraSurface). */
export interface SurfaceRegistry {
  readonly views: SurfaceView[]
  registerView(view: SurfaceView): void
}

declare global {
  interface Window {
    /** The single shared Vue instance the runtime exposes; plugins keep vue external. */
    CalloraVue?: typeof import('vue')
    /** The runtime registry; present once the surface runtime has initialised. */
    calloraSurface?: SurfaceRegistry
  }
}

/**
 * Registers a view with the surface runtime. The runtime bundle initialises the
 * registry before it loads any plugin (chain loader runs after mount), so by the time
 * a plugin executes the registry is present. If it is somehow absent the call is a
 * no-op with a warning rather than a crash — a plugin must never break the shell.
 */
export function registerSurfaceView(view: SurfaceView): void {
  const registry = window.calloraSurface
  if (!registry) {
    console.warn(
      `[callora-surface-sdk] surface runtime not initialised; view "${view.id}" was not registered.`,
    )
    return
  }

  registry.registerView(view)
}
