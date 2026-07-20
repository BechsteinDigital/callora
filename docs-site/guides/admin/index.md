# Extending the admin shell

The **admin shell** is Callora's operator UI — the backoffice SPA where an operator
manages users, roles, workspaces, plugins, media, jobs, and system config. It is a
colocated **Vue 3** application at
`src/Administration/Resources/app/administration/` (Radix Vue components, SCSS design
tokens on `--cal-*`), built to `wwwroot/admin` and served at **`/admin`** via static web
assets.

The shell is **fixed** — a plugin does not replace it or ship its own admin app. Instead a
plugin *extends* the shell from the outside through a small, explicit client-side contract:
the global **`window.CalloraAdmin`**, installed by the shell's extension loader
(`src/core/extensions/loader.ts`) before the app mounts. Your plugin ships one JavaScript
bundle that registers against that global as it loads.

There are exactly **three** extension mechanisms, and each answers a different question.

## What you'll learn

- What the admin shell is, and why it is a fixed shell rather than a replaceable app
- The three extension mechanisms — **slots**, **hooks**, and **service overrides** — and
  when to reach for each
- How an admin bundle is built (Vue kept external, resolved from the shell) and how it is
  published and loaded at `/admin`
- How admin extensions differ from **surface** plugins, and a learning path through the
  rest of these guides

## The three mechanisms

Everything a plugin does to the admin shell goes through one global object. Its shape is
declared in `loader.ts` as `CalloraAdminGlobal`:

```ts
interface CalloraAdminGlobal {
  registerExtension(slot: string, component: Component, order?: number): void
  registerHook<T>(name: string, handler: (ctx: HookContext<T>) => void | Promise<void>, order?: number): void
  registerService<T>(key: string, implementation: T, meta?: { priority?: number }): void
  vue: { h: typeof h; defineComponent: typeof defineComponent }   // shared Vue primitives
}
```

- **Slots** (`registerExtension`) — *additive UI*. Drop a component into a named position
  in the shell. Every contribution renders; nothing is replaced.
- **Hooks** (`registerHook`) — *before/after interception*. Observe an operator action, and
  for `before-*` hooks **cancel** or mutate it.
- **Service overrides** (`registerService`) — *exclusive replacement*. Swap out a named
  shell service (e.g. the users API) with your own implementation. Only one wins.

The shell also exposes `vue` on the global — the **shared** `h` and `defineComponent`
primitives — so your bundle builds components against the *same* Vue instance the shell
runs, instead of bundling its own.

### Which one do I use?

| You want to… | Mechanism | Nature | Guide |
| --- | --- | --- | --- |
| Add a widget, panel, field, or toolbar button into an existing view | **Slot** | Additive — many coexist | [Slots](./slots) |
| Add a row action or metric to a list/dashboard | **Slot** | Additive | [Slots](./slots) |
| Veto a save/delete/activate, or enrich its payload first | **Hook** (`before-*`) | Cancelable, ordered | [Hooks](./hooks) |
| React *after* an action succeeds (audit, toast, refresh) | **Hook** (`after-*`) | Observe-only | [Hooks](./hooks) |
| Replace how the shell talks to a backend (custom users/roles API) | **Service override** | Exclusive — one wins | [Service overrides](./service-overrides) |

A useful rule of thumb: **slots add, hooks intervene, services replace.** If two plugins
want to do the same additive thing, slots let them coexist; if two plugins override the same
service, only the highest-priority one wins and the conflict is reported (see
[Service overrides](./service-overrides)).

## How an admin bundle is built and loaded

An admin extension is **one self-registering JavaScript bundle** that the shell loads at
runtime. There is no build-time dependency on the shell — the bundle only touches the global
`window.CalloraAdmin`. The lifecycle:

1. **Author** an entry module that calls `CalloraAdmin.registerExtension` /
   `registerHook` / `registerService` at top level.
2. **Build** it to your plugin's `src/Resources/public/admin/` directory, keeping Vue
   external so your components run on the shell's Vue instance.
3. **Publish** — when the plugin is active, `PluginUiAssetPublisher` copies
   `src/Resources/public/admin/` to `<webroot>/plugin-assets/<pluginId>/app/admin/` and
   records the entry in the UI-asset manifest at
   `/manifests/plugin-ui-assets.manifest.json` (`surface: "admin"`).
4. **Load** — on first load of `/admin`, the shell's loader (`loadPluginExtensions` in
   `loader.ts`) installs `window.CalloraAdmin`, fetches the manifest, and injects each
   plugin's admin script (and any style) **before** mounting the app. Your registrations
   are present on first render.

::: info Vue is external — the shell owns the Vue instance
Your bundle keeps `vue` external. Build your components with the shared
`CalloraAdmin.vue.defineComponent` / `CalloraAdmin.vue.h` primitives so they run inside the
*same* Vue instance as the shell — no duplicate Vue, no cross-instance reactivity bugs. This
mirrors how surface plugins share `window.CalloraVue`.
:::

::: warning Register synchronously, at top level
The loader attributes every registration to the plugin whose bundle is *currently loading*.
That attribution window is **synchronous**: a call deferred via `setTimeout` or a dynamic
`import()` runs after the window closes and is recorded with a `null` owner
(indistinguishable from a host registration). Always register at bundle top level.
:::

The complete, buildable end-to-end example lives in
[Building an admin module](./building-an-admin-module).

## Admin vs. surface — two runtimes, two audiences

Callora has two front-end runtimes, and they are deliberately kept apart (ADR-014 §10.3 —
the admin shell is decoupled from the surface compiler and evolves independently):

| | **Admin shell** | **Surface** |
| --- | --- | --- |
| Audience | The **operator** (backoffice) | The **tenant's** end users |
| Shape | One fixed, opinionated Vue 3 SPA | Neutral, per-workspace rendered surfaces |
| Extend via | `window.CalloraAdmin` (slots / hooks / services) | `@callora/surface-sdk` (`registerSurfaceView`) |
| Shared Vue | `window.CalloraAdmin.vue` (`h`, `defineComponent`) | `window.CalloraVue` |
| Published under | `/plugin-assets/<id>/app/admin/` | `/plugin-assets/<id>/app/workspace/` |
| Served at | `/admin` | the workspace's surface route (`/surface/render`) |

Use the **admin shell** to give operators new backoffice capabilities; use a **surface
plugin** to build the tenant-facing website or portal. The two share the same publish/load
plumbing but nothing else. For the surface side, start at
[Surfaces](/guides/surface/) and
[Building a surface plugin](/guides/surface/building-a-surface-plugin).

## Learning path

1. **[Slots](./slots)** — the most common extension: add UI into a named position.
2. **[Hooks](./hooks)** — intercept and veto operator actions.
3. **[Service overrides](./service-overrides)** — replace a shell service exclusively.
4. **[Building an admin module](./building-an-admin-module)** — a complete, buildable
   bundle that uses all three, from `main.ts` to loaded at `/admin`.

## Next steps

- Add your first widget: **[Slots](./slots)**
- The full worked bundle: **[Building an admin module](./building-an-admin-module)**
- How a plugin exports its extensions (backend): **[Exporting extensions](/guides/fundamentals/exporting-extensions)**
- The two-runtime architecture: **[Architecture](/concepts/architecture)**
- The tenant-facing counterpart: **[Surfaces](/guides/surface/)**
