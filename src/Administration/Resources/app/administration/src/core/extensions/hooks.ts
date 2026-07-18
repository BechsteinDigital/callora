// Extension hooks: plugins register handlers around core actions to INTERVENE
// (cancel or mutate) rather than only add UI. This is the controlled alternative
// to Shopware component-method overrides — explicit, named hook points instead of
// coupling to internal implementations. Hook names are a public contract
// ("{module}.{before|after}-{action}"); keep them stable.

export interface HookContext<T> {
  // Mutable: a handler may enrich/adjust the payload in place before the action
  // proceeds. For "after" hooks it is read-only in practice.
  readonly payload: T
  // Aborts the action; the first cancel wins and stops later handlers.
  cancel(reason?: string): void
}

export interface HookOutcome {
  readonly canceled: boolean
  readonly cancelReason?: string
}

type HookHandler<T> = (ctx: HookContext<T>) => void | Promise<void>

interface HookRegistration {
  readonly handler: HookHandler<unknown>
  readonly order: number
}

const hooks = new Map<string, HookRegistration[]>()

export function registerHook<T>(name: string, handler: HookHandler<T>, order = 0): void {
  const list = hooks.get(name) ?? []
  list.push({ handler: handler as HookHandler<unknown>, order })
  hooks.set(name, list)
}

// Runs every handler for `name` in ascending order, awaiting each. Handlers see
// the same `payload` (mutations persist) and may cancel; a cancel short-circuits
// the remaining handlers. Returns whether the action was canceled.
export async function runHook<T>(name: string, payload: T): Promise<HookOutcome> {
  let canceled = false
  let cancelReason: string | undefined
  const ctx: HookContext<T> = {
    payload,
    cancel(reason?: string) {
      canceled = true
      cancelReason = reason
    },
  }

  const list = [...(hooks.get(name) ?? [])].sort((a, b) => a.order - b.order)
  for (const { handler } of list) {
    await handler(ctx as HookContext<unknown>)
    if (canceled) {
      break
    }
  }

  return { canceled, cancelReason }
}

export function resetHooks(): void {
  hooks.clear()
}
