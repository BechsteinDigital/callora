// Service-override registry: a plugin may REPLACE a named core service (e.g. swap
// in its own user backend). Views resolve services via `useService(key, fallback)`
// where the fallback is the built-in implementation. Only services deliberately
// wired through this registry are overridable — a narrow, explicit contract, not
// blanket monkey-patching. Service keys are a public contract; keep them stable.

const overrides = new Map<string, unknown>()

export function registerService<T>(key: string, implementation: T): void {
  overrides.set(key, implementation)
}

export function useService<T>(key: string, fallback: T): T {
  return (overrides.get(key) as T | undefined) ?? fallback
}

export function resetServices(): void {
  overrides.clear()
}
