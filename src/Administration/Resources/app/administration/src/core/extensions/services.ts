// Service-override registry: a plugin may REPLACE a named core service (e.g. swap
// in its own user backend). Views resolve services via `useService(key, fallback)`
// where the fallback is the built-in implementation. Only services deliberately
// wired through this registry are overridable — a narrow, explicit contract, not
// blanket monkey-patching. Service keys are a public contract; keep them stable.
//
// A service is EXCLUSIVE (only one implementation wins), so overrides are tracked
// per key with an owning plugin and a priority. The winner is the highest priority
// (ties resolve to the last registered — deterministic given the loader's ordered,
// sequential plugin loading). Every registration is retained so a conflict can be
// surfaced to the operator instead of silently swallowed.

export interface ServiceRegistrationMeta {
  readonly pluginId?: string | null
  readonly priority?: number
}

interface ServiceRegistration {
  readonly pluginId: string | null
  readonly priority: number
  readonly implementation: unknown
}

const registrations = new Map<string, ServiceRegistration[]>()

export function registerService<T>(key: string, implementation: T, meta: ServiceRegistrationMeta = {}): void {
  const list = registrations.get(key) ?? []
  list.push({ pluginId: meta.pluginId ?? null, priority: meta.priority ?? 0, implementation })
  registrations.set(key, list)
}

function winner(key: string): ServiceRegistration | undefined {
  const list = registrations.get(key)
  if (!list || list.length === 0) {
    return undefined
  }
  // Highest priority wins; `>=` lets a later equal-priority registration win.
  return list.reduce((best, current) => (current.priority >= best.priority ? current : best))
}

export function useService<T>(key: string, fallback: T): T {
  return (winner(key)?.implementation as T | undefined) ?? fallback
}

// Diagnostics: a key overridden by more than one registration. `activePluginId`
// owns the winning implementation; `shadowedPluginIds` are the ones it beat.
export interface ServiceConflict {
  readonly key: string
  readonly activePluginId: string | null
  readonly shadowedPluginIds: (string | null)[]
}

export function getServiceConflicts(): ServiceConflict[] {
  const conflicts: ServiceConflict[] = []
  for (const [key, list] of registrations) {
    if (list.length < 2) {
      continue
    }
    const active = winner(key)
    conflicts.push({
      key,
      activePluginId: active?.pluginId ?? null,
      shadowedPluginIds: list.filter((registration) => registration !== active).map((registration) => registration.pluginId),
    })
  }
  return conflicts
}

export function resetServices(): void {
  registrations.clear()
}
