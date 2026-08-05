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
  caller: SurfaceCaller
}

/** Who a surface request belongs to. Stable identity is issuer + subjectId. */
export interface SurfaceSubject {
  issuer: string
  subjectId: string
}

/**
 * Who is using the surface. A caller always exists: between anonymous and logged in
 * sits the recognised guest, which is what a cart or a multi-step form hangs off.
 * The two states are a discriminated union so code cannot mistake the presence of a
 * subject for authentication.
 */
export type SurfaceCaller =
  | { state: 'guest'; subject: SurfaceSubject }
  | {
      state: 'authenticated'
      subject: SurfaceSubject
      displayName: string
      claims: Record<string, string[]>
    }

/**
 * Instance parameters an island carries: what the SSR template passed at the slot's
 * call site, so an embedded view can point at a concrete lead, room or appointment
 * instead of deriving everything from the URL.
 */
export type SurfaceViewParams = Readonly<Record<string, unknown>>

/** A view a plugin contributes — rendered as the whole app or into a matching island. */
export interface SurfaceView {
  /** Stable, unique id. Also the value of data-callora-island for island mounts. */
  id: string
  /**
   * The Vue component. Receives the SurfaceContext as a `context` prop and the
   * island's instance parameters as a `params` prop.
   */
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
