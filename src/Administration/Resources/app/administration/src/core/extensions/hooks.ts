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

/** One handler that threw, with the plugin it belongs to. */
export interface HookFailure {
  readonly pluginId: string | null
  readonly error: unknown
}

export interface HookOutcome {
  readonly canceled: boolean
  readonly cancelReason?: string
  // Handlers that threw. For "after" hooks the action still succeeded — a caller that
  // wants to say so has the material here, and one that does not is unaffected.
  readonly failures: readonly HookFailure[]
}

type HookHandler<T> = (ctx: HookContext<T>) => void | Promise<void>

interface HookRegistration {
  readonly handler: HookHandler<unknown>
  readonly order: number
  // Owning plugin (set by the loader), or null for a host/test registration.
  readonly pluginId: string | null
}

const hooks = new Map<string, HookRegistration[]>()

export function registerHook<T>(name: string, handler: HookHandler<T>, order = 0, pluginId: string | null = null): void {
  const list = hooks.get(name) ?? []
  list.push({ handler: handler as HookHandler<unknown>, order, pluginId })
  hooks.set(name, list)
}

// Runs every handler for `name` in ascending order, awaiting each. Handlers see
// the same `payload` (mutations persist) and may cancel; a cancel short-circuits
// the remaining handlers. Returns whether the action was canceled.
//
// A handler that THROWS is handled here rather than at the ~25 call sites, and the two
// halves of the hook contract need opposite answers:
//
//   before-*  The handler stands for a check — "may this happen?". Carrying on past a
//             thrown exception would skip exactly the check it represents, so an
//             exception counts as a cancel. Fail-closed, and the same default for any
//             name that fits neither half.
//   after-*   The action already succeeded. There is nothing left to cancel, so the
//             exception is recorded and the next handler runs. Before this, the throw
//             propagated into the call site's own `try` — which sits AFTER the successful
//             server call — and the operator was shown a failure for something that had
//             fully worked: file uploaded, red box, input not cleared, list not reloaded
//             (#289).
export async function runHook<T>(name: string, payload: T): Promise<HookOutcome> {
  let canceled = false
  let cancelReason: string | undefined
  const failures: HookFailure[] = []
  const ctx: HookContext<T> = {
    payload,
    cancel(reason?: string) {
      canceled = true
      cancelReason = reason
    },
  }

  const actionAlreadyHappened = name.includes('.after-')
  const list = [...(hooks.get(name) ?? [])].sort((a, b) => a.order - b.order)
  for (const { handler, pluginId } of list) {
    try {
      await handler(ctx as HookContext<unknown>)
    } catch (error) {
      failures.push({ pluginId, error })
      // The plugin id is the point of logging here at all: without it the operator sees a
      // broken screen and no way to tell which extension caused it. The registration has
      // always known it (see HookRegistration) — it just never reached anyone.
      console.error(
        `[callora-admin] hook handler for "${name}" from ${pluginId ?? 'host'} threw.`,
        error,
      )

      if (!actionAlreadyHappened) {
        canceled = true
        cancelReason ??= `Eine Erweiterung (${pluginId ?? 'unbekannt'}) hat die Aktion nicht zugelassen.`
        break
      }
    }

    if (canceled) {
      break
    }
  }

  return { canceled, cancelReason, failures }
}

export function resetHooks(): void {
  hooks.clear()
}
