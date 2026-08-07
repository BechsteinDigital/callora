# @callora/admin

The typed contract a Callora **admin plugin** builds against: extension points, the shell's
UI primitives, and the design tokens they style themselves from.

```bash
npm install @callora/admin
```

Apache-2.0. Your plugin may carry any licence you like, including a proprietary one.

## What it gives you

```ts
import { registerAdminPage } from '@callora/admin/extensions'
import { CalPage, CalButton, CalDataTable } from '@callora/admin/components'
```

| Entry point | What |
|---|---|
| `@callora/admin` | The package contract and its version constant |
| `@callora/admin/extensions` | Extension points — where a plugin may attach |
| `@callora/admin/components` | The shell's primitives: `CalPage`, `CalButton`, `CalDataTable`, … |
| `@callora/admin/tokens` | The `--cal-*` design tokens |
| `@callora/admin/patterns` | Composed patterns built from the primitives |

## Why use the primitives

They style themselves entirely through `--cal-*` tokens, so a plugin page looks like the
shell without copying a single colour — and follows a theme change without being rebuilt.
That is the reason to reach for them over hand-rolled markup, more than the saved effort.

A plugin running **inside** the shell needs no stylesheet of its own: Vue derives the
scoped-style ids from file paths, so this library's build produces the same `data-v-*`
attributes the shell already carries styles for. `@callora/admin/style.css` exists for the
other case — a Storybook, an isolated test — where no shell has loaded them.

## Peer dependencies

`vue`, `vue-router`, `radix-vue` and `lucide-vue-next` are **peers**, not dependencies. The
shell provides Vue to plugin bundles at runtime through `window.CalloraAdmin.vue`; a second
copy in your tree would give you two Vue runtimes and a component that never renders.

## Building an admin plugin

Your bundle is an IIFE with Vue marked external. The full walkthrough:
[Backend extensions](https://github.com/BechsteinDigital/callora/blob/main/docs-site/guides/backend-extensions.md)
and the plugin guides in the same repository.

## Extension-point catalog

The catalog under `extensions` is **generated from the shell**, and CI fails if it drifts.
A slot that moves without regeneration would leave this package promising a point that no
longer exists — and the type error would land on the plugin author, not on us.
