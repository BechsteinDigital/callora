# Slots — additive admin UI

A **slot** is a named position in the admin shell where a plugin can drop its own Vue
component. Slots are **additive**: every component registered for a slot renders (in order),
and nothing the shell already shows is replaced. This is Callora's Vue-3 answer to
Shopware-style component overrides — you extend named, public positions instead of coupling
to the shell's internal structure.

Slots are the right tool for adding a widget, a panel, a field, a toolbar button, or a
per-row action to an existing admin view.

## What you'll learn

- How a slot is defined in the shell and how `<ExtensionSlot>` renders contributions
- How to register a component into a slot with `registerExtension(slot, component, order?)`
- How slot **context** (`ctx`) reaches your component, and which slots pass what
- How **ordering** works when several plugins target the same slot
- How to discover the slot names available to you

::: tip Prerequisites

- You have an admin bundle that runs against `window.CalloraAdmin` — see
  [Building an admin module](./building-an-admin-module) for the full build/publish setup.
- You know the **slot name** you want to target (see [Discovering slot names](#discovering-slot-names)).
:::

## How slots work

On the shell side, a view marks a slot with the `<ExtensionSlot>` component
(`src/core/extensions/ExtensionSlot.vue`). It renders every component registered for that
name, passing an optional `ctx` prop through to each:

```vue
<!-- shell view, e.g. UserDetailView.vue -->
<ExtensionSlot name="users.detail.fields" :ctx="{ userId }" />
```

`ExtensionSlot` is a thin wrapper over the registry (`src/core/extensions/registry.ts`):

```vue
<template>
  <component :is="component" v-for="(component, i) in components" :key="i" :ctx="ctx" />
</template>
```

An **empty slot renders nothing** — if no plugin contributes, the operator sees no gap.

On the plugin side, you push a component into that slot:

```ts
CalloraAdmin.registerExtension('users.detail.fields', MyComponent, 20)
```

The registry keeps every registration and, when the slot renders, returns them sorted by
ascending `order` (`getExtensions`). Because slots are additive there is no conflict to
resolve — multiple plugins contributing to `users.detail.fields` all show up, side by side.

## A worked example — a panel on the user detail view

Add a small "recent activity" panel below the built-in fields on the user detail page. The
shell passes `{ userId }` as `ctx` to the `users.detail.fields` slot (confirmed in
`UserDetailView.vue`), so your panel knows which user it is looking at.

Build the component with the shell's shared Vue primitives (`CalloraAdmin.vue`) so it runs
on the same Vue instance — no bundled Vue:

```ts
// src/main.ts — runs at bundle load
const { h, defineComponent } = CalloraAdmin.vue

const RecentActivityPanel = defineComponent({
  name: 'RecentActivityPanel',
  // ExtensionSlot passes the slot ctx through as the `ctx` prop.
  props: { ctx: { type: Object, default: () => ({}) } },
  setup(props) {
    const userId = () => (props.ctx as { userId?: string }).userId ?? '(unknown)'
    return () =>
      h('section', { class: 'acme-activity-panel' }, [
        h('h3', 'Recent activity'),
        h('p', `Activity for user ${userId()}`),
      ])
  },
})

// Register into the slot. order 20 → renders after lower-order contributions.
CalloraAdmin.registerExtension('users.detail.fields', RecentActivityPanel, 20)
```

**Expected result:** open a user in `/admin`, and below the built-in fields you see your
"Recent activity" panel, with the correct `userId` filled in from the slot context. If
another plugin also contributes to this slot, both panels render, ordered by their `order`.

::: info Read `ctx` defensively
`ctx` is optional on `<ExtensionSlot>` and its shape is defined by the shell view that
declares the slot. Default it (`default: () => ({})`) and read the fields you expect — don't
assume it is always populated.
:::

## Context per slot

`ctx` is whatever the shell view chooses to pass. It is part of the slot's contract. Some
representative slots and what they pass (from the shell views):

| Slot | `ctx` shape | Where |
| --- | --- | --- |
| `users.detail.fields` | `{ userId }` | User detail |
| `users.list.row-actions` | the user row | Users list (per row) |
| `dashboard.metrics` | `{ permissions: string[] }` | Dashboard |
| `workspaces.detail.fields` | `{ workspaceKey }` | Workspace detail |
| `plugins.list.row-actions` | the plugin row | Plugins list (per row) |
| `themes.after-assignment` | `{ workspaceKey, assignment }` | Themes |
| `config.fields` | `{ pluginId }` | System config |

A **`*.list.toolbar`** slot (e.g. `users.list.toolbar`, `plugins.list.toolbar`) is passed no
`ctx` — it is a place to add a toolbar button. A **`*.row-actions`** slot renders once per
row and receives that row as `ctx`.

## Ordering

`order` is ascending; the default is `0`. Ties preserve registration order (the registry's
sort is stable). So:

```ts
CalloraAdmin.registerExtension('users.list.toolbar', ExportButton, 10)
CalloraAdmin.registerExtension('users.list.toolbar', ImportButton, 20)
// → ExportButton renders before ImportButton
```

Because plugin bundles are loaded **sequentially in a deterministic order**, two plugins
that both use `order: 0` render in load order. Give your contribution an explicit `order`
when its position matters.

::: warning Slot names are a public contract
Slot IDs follow the `"{module}.{view}.{position}"` convention and are treated as a stable,
public contract by the shell (`registry.ts`). Target the exact string; a typo silently
lands your component in a slot no view renders, and nothing shows up.
:::

## Discovering slot names

There is no runtime "list slots" call — slot names are declared where the shell renders them.
To find the slot you need:

- **Search the shell source** for `<ExtensionSlot` under
  `src/Administration/Resources/app/administration/src/modules/`. Each occurrence gives you
  the exact `name` and the `:ctx` it passes.
- **Follow the naming convention** — `"{module}.{view}.{position}"`. For a list view expect a
  `.list.toolbar` and a `.list.row-actions`; for a detail view expect a `.detail.fields`.
- **Consult the extension reference** for the catalogued slot names and their context shapes:
  [Extension manifests & contracts](/reference/extension-manifests).

> **Status:** A published, versioned catalogue of admin slot names (with `ctx` schemas) is
> planned but not yet part of the reference docs. Until then, the shell source under
> `modules/**/*.vue` is the authoritative list of available slots.

## Next steps

- Intercept an action instead of just adding UI: **[Hooks](./hooks)**
- Replace a shell service: **[Service overrides](./service-overrides)**
- Put it all together in a buildable bundle: **[Building an admin module](./building-an-admin-module)**
- The slot-name catalogue: **[Extension manifests & contracts](/reference/extension-manifests)**
