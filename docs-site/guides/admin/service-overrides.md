# Service overrides — replacing a shell service

Slots are additive and hooks intervene; **service overrides** are **exclusive**. A plugin
may *replace* a named shell service — for example, swap in its own users backend — and only
**one** implementation is ever live for a given key. This is a narrow, deliberate contract:
only services the shell deliberately wires through this registry are overridable, not blanket
monkey-patching.

Reach for a service override when you need to change *how the shell talks to a backend*
(different API, different transport, a mock) rather than change the UI or veto an action.

## What you'll learn

- How the shell resolves a service with `useService(key, fallback)`
- How to register an override with `registerService(key, impl, meta?)`
- How **priority** picks the winner, and how ties resolve
- How conflicts (two plugins overriding the same key) are surfaced via
  `getServiceConflicts()`
- A worked override of the users API

::: tip Prerequisites

- An admin bundle running against `window.CalloraAdmin` — see
  [Building an admin module](./building-an-admin-module).
- The **service key** you want to override and the **interface** the shell expects of it
  (see [Discovering service keys](#discovering-service-keys)).
:::

## How service overrides work

On the shell side, a view resolves a service through `useService(key, fallback)`
(`src/core/extensions/services.ts`), passing the **built-in implementation** as the fallback:

```ts
// shell view, e.g. UserDetailView.vue
import { usersApi } from '@/…/usersApi'
const api = useService('usersApi', usersApi)   // plugin override, or the built-in fallback
```

`useService` returns the **winning override** for that key, or the `fallback` if no plugin
registered one. So with no plugins installed the shell simply uses its own service — nothing
changes.

Your plugin registers an override:

```ts
CalloraAdmin.registerService('usersApi', new MyUsersApi(), { priority: 10 })
```

The registry keeps **every** registration for a key (it does not discard the losers) so a
conflict can be surfaced to the operator instead of silently swallowed.

### Priority resolution

The winner is the registration with the **highest `priority`** (default `0`). The rule is
`>=`, so a later registration wins a **tie** — deterministic given the loader's ordered,
sequential plugin loading (`services.ts`, verified in `services.test.ts`):

```ts
registerService('usersApi', high, { pluginId: 'a', priority: 100 })
registerService('usersApi', low,  { pluginId: 'b', priority: 10 })
useService('usersApi', core)   // → high (priority 100 wins, regardless of order)

// tie → last registered wins
registerService('usersApi', first,  { pluginId: 'a' })   // priority 0
registerService('usersApi', second, { pluginId: 'b' })   // priority 0
useService('usersApi', core)   // → second
```

You do **not** pass `pluginId` — the loader injects the owning plugin id for you (you only
supply an optional `priority`). Keys are isolated: overriding `rolesApi` does not touch
`usersApi`.

## A worked example — override the users API

Replace the built-in users backend with one that talks to your own endpoint. Your
implementation must satisfy the **same interface** the shell's `usersApi` exposes (the shell
calls methods like `save`, `delete`, `list` on whatever `useService` returns).

```ts
// src/main.ts — runs at bundle load

// Must expose the same shape the shell expects of `usersApi`.
class AcmeUsersApi {
  async list() {
    const res = await fetch('/acme-api/users', { credentials: 'include' })
    return res.json()
  }
  async save(draft: { email: string; displayName?: string }) {
    const res = await fetch('/acme-api/users', {
      method: 'POST',
      credentials: 'include',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(draft),
    })
    return res.json()
  }
  async delete(userId: string) {
    await fetch(`/acme-api/users/${userId}`, { method: 'DELETE', credentials: 'include' })
  }
}

// priority 10 beats the shell default (priority 0) and any lower-priority plugin.
CalloraAdmin.registerService('usersApi', new AcmeUsersApi(), { priority: 10 })
```

**Expected result:** every shell view that resolves `useService('usersApi', usersApi)` — the
users list, the user detail view — now calls **your** `AcmeUsersApi` instead of the built-in
one. If no other plugin registers a higher priority, yours is live everywhere the key is
used.

::: warning Match the service contract exactly
A service override is exclusive and un-typed at the boundary — the registry stores your
implementation as `unknown`. If your object is missing a method the shell calls, the shell
will fail at call time, not registration time. Implement the **full** interface the shell
expects for that key.
:::

## Reporting conflicts

When more than one plugin overrides the same key, that's a **conflict** — only one wins, and
the operator should be able to see which. `getServiceConflicts()` reports exactly that
(`services.ts`, verified in `services.test.ts`):

```ts
registerService('usersApi', a, { pluginId: 'a', priority: 1 })
registerService('usersApi', b, { pluginId: 'b', priority: 5 })

getServiceConflicts()
// → [{ key: 'usersApi', activePluginId: 'b', shadowedPluginIds: ['a'] }]
```

Each conflict names the **`activePluginId`** (the winner) and the **`shadowedPluginIds`** it
beat. A single registration produces **no** conflict — overriding a service on your own is
the normal case, not an error.

::: info The winner can shadow the host default
The shell's own default is a registration too. If your plugin overrides `usersApi`, the
conflict report shows your plugin as active and the host default (`pluginId: null`) as
shadowed. That is expected — it is how the operator sees that a plugin has taken over a
built-in service.
:::

## Discovering service keys

Overridable service keys follow no fixed prefix, but each is passed as the first argument to
`useService(...)` in a shell view. To find them:

- **Search the shell source** for `useService(` under
  `src/Administration/Resources/app/administration/src/modules/`. Each call site gives you the
  key **and** the fallback implementation whose interface you must match.

Keys wired through the registry today include: `usersApi`, `rolesApi`, `workspacesApi`,
`pluginsApi`, `mediaApi`, `jobsApi`, `tenantsApi`, `webhooksApi`, `entitlementsApi`,
`flowsApi`, `themesApi`, and `systemConfigApi`.

> **Status:** A published, versioned catalogue of overridable service keys and their required
> interfaces is planned but not yet in the reference docs. Until then, the `useService(` call
> sites in the shell source are the authoritative list, and the fallback passed at each call
> site is the interface you must implement.

## Next steps

- Add UI additively: **[Slots](./slots)**
- Intercept an action: **[Hooks](./hooks)**
- A complete bundle using an override: **[Building an admin module](./building-an-admin-module)**
- The service-key catalogue: **[Extension manifests & contracts](/reference/extension-manifests)**
- The shell's REST endpoints your override may call: **[REST API](/reference/rest-api)**
