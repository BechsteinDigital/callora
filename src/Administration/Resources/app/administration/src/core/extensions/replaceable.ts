import type { Component } from 'vue'

/**
 * Component replacement — deliberately NOT blanket override.
 *
 * Shopware-style `Component.override` couples a plugin to the internal structure of whatever it
 * overrides, which is exactly what the slot registry avoids ("additive slots, no
 * internal-structure coupling"). Here a component declares that it is replaceable, and its prop
 * contract is the boundary: a replacement must satisfy the same props, and TypeScript says so.
 *
 * Where a named slot suffices, that stays the better answer — replacement is the exception, for
 * the case where the whole rendering has to differ.
 *
 * Mirrors the service registry deliberately: exclusive (one implementation wins),
 * priority-ordered, and conflicts surfaced rather than swallowed.
 */

/**
 * A replaceable component: its key and the implementation to fall back on.
 *
 * Deliberately a wrapper rather than a branded component. Branding would mean writing the key
 * onto the component object, which mutates the caller's argument — and a component registered
 * under two keys would silently keep only the last one. A wrapper also makes the intent visible:
 * a token is not something you render, it is something you resolve.
 */
export interface ReplaceableComponent<T extends Component> {
  readonly key: string
  readonly base: T
}

export interface ReplacementMeta {
  /** Owning plugin, set by the loader. Null for a host or test registration. */
  readonly pluginId?: string | null
  /** Highest priority wins; ties resolve to the last registration. */
  readonly priority?: number
}

interface Registration {
  readonly pluginId: string | null
  readonly priority: number
  readonly implementation: Component
}

const registrations = new Map<string, Registration[]>()

/**
 * Declares a component replaceable under a key. The returned token carries both, so a consumer
 * resolves without repeating the key — and cannot accidentally pass a different one.
 */
export function defineReplaceable<T extends Component>(
  key: string,
  base: T,
): ReplaceableComponent<T> {
  return { key, base }
}

export function replaceComponent(
  key: string,
  implementation: Component,
  meta: ReplacementMeta = {},
): void {
  const list = registrations.get(key) ?? []
  list.push({ pluginId: meta.pluginId ?? null, priority: meta.priority ?? 0, implementation })
  registrations.set(key, list)
}

function winner(key: string): Registration | undefined {
  const list = registrations.get(key)
  if (!list || list.length === 0) {
    return undefined
  }
  // `>=` lets a later equal-priority registration win — deterministic given the loader's
  // ordered, sequential plugin loading.
  return list.reduce((best, current) => (current.priority >= best.priority ? current : best))
}

/** Resolves the component to render: the winning replacement, or the declared original. */
export function useComponent<T extends Component>(token: ReplaceableComponent<T>): Component {
  return winner(token.key)?.implementation ?? token.base
}

export interface ComponentConflict {
  readonly key: string
  readonly activePluginId: string | null
  readonly shadowedPluginIds: (string | null)[]
}

/**
 * A key replaced by more than one plugin. Two plugins replacing the same component is a
 * composition mistake somebody has to be able to see — swallowing it would leave an operator
 * wondering why one plugin's UI never appears.
 */
export function getComponentConflicts(): ComponentConflict[] {
  const conflicts: ComponentConflict[] = []
  for (const [key, list] of registrations) {
    if (list.length < 2) {
      continue
    }
    const active = winner(key)
    conflicts.push({
      key,
      activePluginId: active?.pluginId ?? null,
      shadowedPluginIds: list.filter((registration) => registration !== active).map((r) => r.pluginId),
    })
  }
  return conflicts
}

/** Test/hot-reload aid — clears all replacements. */
export function resetReplacements(): void {
  registrations.clear()
}
