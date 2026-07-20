# Hooks — intercepting operator actions

Where slots *add* UI, **hooks** let a plugin step into an operator action to **observe**,
**mutate**, or **veto** it. The shell wraps its important actions (save, delete, activate,
upload, …) in named hook points and `await`s every registered handler around the action.
This is the controlled alternative to Shopware component-method overrides — explicit, named
hook points instead of coupling to the shell's internal implementations.

A hook comes in two flavours by convention:

- **`before-*`** — runs *before* the action. Handlers may **cancel** it (aborting the action)
  or **mutate** the payload the action will use.
- **`after-*`** — runs *after* the action succeeded. Observe-only in practice (audit, toast,
  refresh a list).

## What you'll learn

- How the shell defines hook points and runs handlers (`registerHook` + `runHook`)
- The `HookContext<T>` a handler receives — reading, mutating, and cancelling
- How **ordering**, **async**, and **cancel short-circuiting** behave
- A worked example that vetoes a user save under a business rule

::: tip Prerequisites

- An admin bundle running against `window.CalloraAdmin` — see
  [Building an admin module](./building-an-admin-module).
- The **hook name** you want to target (see [Discovering hook names](#discovering-hook-names)).
:::

## How hooks work

The runtime lives in `src/core/extensions/hooks.ts`. A shell action calls `runHook(name,
payload)` and inspects the outcome:

```ts
// shell view, e.g. UserDetailView.vue
const before = await runHook('users.before-save', draft)
if (before.canceled) {
  // action aborted — the shell does not save
  return
}
await api.save(draft)
await runHook('users.after-save', { userId: id })
```

`runHook` builds a single `HookContext<T>`, runs every handler registered for `name` in
**ascending `order`**, awaiting each, and returns `{ canceled, cancelReason }`. A handler
that cancels **short-circuits** the rest — later handlers do not run.

Your plugin registers a handler:

```ts
CalloraAdmin.registerHook<{ email: string }>('users.before-save', (ctx) => {
  // read, mutate, or cancel here
})
```

### The hook context

Every handler receives a `HookContext<T>` (`hooks.ts`):

```ts
interface HookContext<T> {
  readonly payload: T                 // the action's data — mutable in `before-*`
  cancel(reason?: string): void       // abort the action; first cancel wins
}
```

- **`payload`** — the data the action is about to use. In a `before-*` hook you may enrich
  or adjust it **in place**, and the mutation persists for the action and for later handlers.
  For `after-*` hooks it is read-only in practice (the action already ran).
- **`cancel(reason?)`** — aborts the action. The **first** cancel wins, stops later handlers,
  and surfaces as `{ canceled: true, cancelReason }` to the shell.

## A worked example — veto a user save

Block saving any user whose email is outside your company domain, and normalise the email to
lowercase otherwise. The shell passes the editable `draft` as the payload of
`users.before-save` (confirmed in `UserDetailView.vue`).

```ts
// src/main.ts — runs at bundle load
interface UserDraft {
  email: string
  displayName?: string
}

CalloraAdmin.registerHook<UserDraft>(
  'users.before-save',
  (ctx) => {
    const email = ctx.payload.email?.trim() ?? ''

    // Veto: reject external emails. The shell aborts the save.
    if (!email.endsWith('@acme.example')) {
      ctx.cancel('Only @acme.example addresses are allowed')
      return
    }

    // Mutate: normalise the payload in place before the save proceeds.
    ctx.payload.email = email.toLowerCase()
  },
  10, // order — lower runs earlier
)
```

**Expected result:**

- Saving a user with `sam@gmail.com` is **cancelled** — the shell does not call the API, and
  `runHook` returns `{ canceled: true, cancelReason: 'Only @acme.example addresses are allowed' }`.
- Saving `Sam@ACME.example` proceeds, and the persisted email is `sam@acme.example` because
  your handler mutated `ctx.payload` in place.

::: info The shell decides what a cancel *means*
Your handler only sets `canceled` / `cancelReason`. It's the shell view that reads the
outcome and aborts the action (and typically shows the reason to the operator). Every
`before-*` call site checks `before.canceled` and returns early — so a cancel reliably
prevents the action.
:::

## Ordering, async, and short-circuiting

These behaviours are guaranteed by `runHook` (and covered by `hooks.test.ts`):

- **Ordering** — handlers run in ascending `order`; the default is `0`. Ties keep
  registration order.
- **Shared payload** — all handlers for a hook see the *same* `payload` object, so a mutation
  by an earlier handler is visible to later ones (and to the action).
- **Async** — handlers may be `async`; `runHook` `await`s each before running the next. Use
  this to call an API before deciding whether to cancel.
- **Cancel short-circuits** — once any handler cancels, remaining handlers do **not** run.

```ts
// an async before-hook that calls out before deciding
CalloraAdmin.registerHook<{ pluginId: string }>(
  'plugins.before-activate',
  async (ctx) => {
    const ok = await fetch(`/api/licensing/${ctx.payload.pluginId}`).then((r) => r.ok)
    if (!ok) {
      ctx.cancel('not licensed')
    }
  },
)
```

::: warning A cancel stops later handlers — order matters
If your handler must always run (e.g. audit logging), give it a **low `order`** so it runs
before any handler that might cancel. A handler registered after a cancelling one never
fires.
:::

## Discovering hook names

Hook names follow the convention **`"{module}.{before|after}-{action}"`** and are treated as
a stable public contract (`hooks.ts`). The shell fires a matched `before-`/`after-` pair
around each action. A representative set fired by the shell today:

| Action | `before-*` (cancelable) | `after-*` (observe) |
| --- | --- | --- |
| Save a user | `users.before-save` | `users.after-save` |
| Delete a user | `users.before-delete` | `users.after-delete` |
| Save / delete a role | `roles.before-save` / `roles.before-delete` | `roles.after-save` / `roles.after-delete` |
| Save / delete a workspace | `workspaces.before-save` / `workspaces.before-delete` | `workspaces.after-save` / `workspaces.after-delete` |
| Add / remove a workspace member | `workspaces.member.before-save` / `...before-remove` | `...after-save` / `...after-remove` |
| Install / activate / deactivate a plugin | `plugins.before-install` / `plugins.before-activate` / `plugins.before-deactivate` | corresponding `after-*` |
| Upload / delete media | `media.before-upload` / `media.before-delete` | `media.after-upload` / `media.after-delete` |
| Save flows, webhooks, tenants, config, themes, entitlements | `flows.before-save`, `webhooks.before-create`, `tenants.before-create`, `config.before-save`, `themes.before-assign`, `entitlements.before-*` | corresponding `after-*` |

To find them precisely, search the shell source for `runHook(` under
`src/Administration/Resources/app/administration/src/modules/` — each call site is the exact
hook name and its payload shape.

> **Status:** A published, versioned catalogue of hook names and their payload schemas is
> planned but not yet in the reference docs. Until then, the `runHook(` call sites in the
> shell source are the authoritative list.

## Next steps

- Add UI instead of intercepting: **[Slots](./slots)**
- Replace a shell service exclusively: **[Service overrides](./service-overrides)**
- A complete bundle using a hook: **[Building an admin module](./building-an-admin-module)**
- The hook-name catalogue: **[Extension manifests & contracts](/reference/extension-manifests)**
