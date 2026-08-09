import type { Component } from 'vue'
import type { AdminHook, AdminSlot } from '@/core/extensions/catalog.generated'

/**
 * The typed contract an admin plugin registers against.
 *
 * Until now a plugin hand-copied this interface out of the shell's loader and passed slot names
 * as free strings — a typo was a silent no-op that looked like "my plugin does nothing". The
 * names come from the generated catalog, so a wrong one is a compile error.
 *
 * Every call is a no-op with a warning when the shell is absent, never a throw: a plugin must not
 * be able to break the shell it is a guest in. A bundle may load before the shell finished
 * bootstrapping, or outside it entirely (a test, a Storybook), and neither is worth a crash.
 */

export interface HookContext<T> {
  /** Mutable: a handler may enrich or adjust the payload before the action proceeds. */
  readonly payload: T
  /** Aborts the action; the first cancel wins and stops the remaining handlers. */
  cancel(reason?: string): void
}

export type HookHandler<T> = (ctx: HookContext<T>) => void | Promise<void>

export interface ServiceMeta {
  /** Highest priority wins; ties resolve to the last registration. */
  readonly priority?: number
}

/**
 * The shell's global API. The owning pluginId is injected by the loader — authoritative, so a
 * plugin can neither forget nor spoof it.
 */
export interface CalloraAdminApi {
  registerExtension(slot: string, component: Component, order?: number): void
  registerSurfaceTab(id: string, label: string, component: Component, order?: number): void
  registerHook<T>(name: string, handler: HookHandler<T>, order?: number): void
  registerService<T>(key: string, implementation: T, meta?: ServiceMeta): void
  /** Read side of the slot registry, so a component can render into a slot it does not own. */
  getExtensions(slot: string): Component[]
}

export function resolveAdminApi(): CalloraAdminApi | undefined {
  return (globalThis as Record<string, unknown>).CalloraAdmin as CalloraAdminApi | undefined
}

function missing(what: string): void {
  console.warn(`[callora-admin] admin shell not initialised; ${what} was not registered.`)
}

/**
 * Contributes a component to a shell slot. Slots are additive — every registration renders, in
 * ascending order — so there is no conflict to resolve.
 */
export function registerExtension(slot: AdminSlot, component: Component, order?: number): void {
  const api = resolveAdminApi()
  if (!api) {
    missing(`slot "${slot}"`)
    return
  }
  api.registerExtension(slot, component, order)
}

/**
 * Steuert einen Reiter an der Fläche bei, der DIESER App zugewiesen ist.
 *
 * Nicht `registerExtension` mit einem Slot-Namen: Ein Reiter trägt eine Beschriftung, und er
 * gehört genau einer App. Über einen Slot erschiene er an jeder Fläche — nach dem dritten
 * Plugin wäre die Detailansicht unbenutzbar.
 *
 * Die Komponente bekommt denselben Kontext wie jeder Flächen-Slot: `workspaceKey`,
 * `surfaceKey`, `routing`.
 */
export function registerSurfaceTab(
  id: string,
  label: string,
  component: Component,
  order?: number,
): void {
  const api = resolveAdminApi()
  if (!api) {
    missing(`surface tab "${id}"`)
    return
  }
  api.registerSurfaceTab(id, label, component, order)
}

/**
 * Intervenes around a shell action rather than only adding UI. A `before` handler may enrich the
 * payload or cancel the action outright; an `after` handler observes the outcome.
 */
export function registerHook<T>(name: AdminHook, handler: HookHandler<T>, order?: number): void {
  const api = resolveAdminApi()
  if (!api) {
    missing(`hook "${name}"`)
    return
  }
  api.registerHook(name, handler, order)
}

/**
 * Replaces a named shell service — swapping in a different backend, say. Exclusive: only one
 * implementation wins, and a conflict is surfaced to the operator rather than swallowed.
 */
export function registerService<T>(key: string, implementation: T, meta?: ServiceMeta): void {
  const api = resolveAdminApi()
  if (!api) {
    missing(`service "${key}"`)
    return
  }
  api.registerService(key, implementation, meta)
}

/**
 * Contributes a plugin's full admin page, which the shell renders at `/extensions/{pluginId}`.
 * The plugin owns the whole canvas there — no page frame is imposed.
 *
 * Deliberately its own function rather than a `pageSlot(id)` helper passed to registerExtension:
 * that slot name only exists at runtime, so accepting it there would mean widening the parameter
 * to `AdminSlot | string` — which TypeScript collapses to `string`, silently removing the typo
 * protection from every other slot.
 */
export function registerPage(pluginId: string, component: Component, order?: number): void {
  const api = resolveAdminApi()
  if (!api) {
    missing(`page for "${pluginId}"`)
    return
  }
  api.registerExtension(`extension.page.${pluginId}`, component, order)
}

export type { AdminHook, AdminSlot }
