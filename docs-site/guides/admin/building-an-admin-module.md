# Building an admin module

In this tutorial you'll build a complete admin extension from nothing: a single
self-registering bundle that adds a **slot** widget, a **hook** that vetoes a save, and a
**service override** — then build it to your plugin's published assets, and watch the admin
shell load it at `/admin` with no host restart. Everything here uses the same
`window.CalloraAdmin` contract the shell installs before it mounts.

This is the admin companion to
[Building a surface plugin](/guides/surface/building-a-surface-plugin), which covers the
tenant-facing side. The two share the publish/load plumbing but target different runtimes.

## What you'll learn

- How to lay out an admin bundle inside a plugin (`Resources/app/admin` source →
  `Resources/public/admin` deliverable)
- How to register a slot component, a hook, and a service override from one `main.ts`
- How to configure the Vite build so Vue is **external** (resolved from the shell)
- How the built bundle is **published** to `/plugin-assets/<id>/app/admin/` and **loaded**
  at `/admin`

::: tip Prerequisites

- **Node 20+** and a package manager — the admin bundle is a Vite build, independent of the
  .NET backend build.
- **A Callora plugin** to host the bundle. Any plugin works; if you don't have one, scaffold
  one first via [Build your first Callora plugin](/guides/getting-started/your-first-plugin).
- **A running Callora host** in development so you can install/activate the plugin and open
  `/admin`.
- Familiarity with the three mechanisms — [Slots](./slots), [Hooks](./hooks),
  [Service overrides](./service-overrides).
:::

## The mental model

An admin extension ships **one self-registering IIFE bundle** — `main.js`, plus an optional
`main.css` — under `src/Resources/public/admin`. The bundle has no build-time dependency on
the shell: it only touches the global `window.CalloraAdmin` that the shell's loader installs
before mounting. Vue is kept **external** so your components run on the shell's single Vue
instance rather than shipping their own.

The `admin` segment is fixed for admin bundles. The build outputs to
`src/Resources/public/admin`; on publish the host copies that to
`/plugin-assets/<pluginId>/app/admin/`. The shell's loader finds it via the UI-asset
manifest and injects it before the app mounts.

## Step 1 — Lay out the bundle

Inside your plugin, create the admin source tree:

```text
my-plugin/
└── src/
    └── Resources/
        ├── app/
        │   └── admin/                # source (stays with the vendor)
        │       ├── package.json
        │       ├── vite.config.ts
        │       └── src/
        │           └── main.ts
        └── public/
            └── admin/                # build output — the only thing that ships
```

`package.json` for the bundle:

```json
{
  "name": "my-plugin-admin",
  "private": true,
  "type": "module",
  "scripts": {
    "build": "vite build",
    "dev": "vite build --watch"
  },
  "devDependencies": {
    "vite": "^6.0.0",
    "vue": "^3.5.0"
  }
}
```

::: info Vue is a dev dependency, not a runtime one
You need Vue's types to build against the shared `defineComponent` / `h` primitives, but the
build marks it **external**, so Vue is never bundled. At runtime you use the primitives the
shell hands you on `window.CalloraAdmin.vue`.
:::

## Step 2 — Configure the build

The admin bundle is an **IIFE** with Vue external. Unlike the surface side (which ships a
blessed `@callora/surface-sdk/vite-preset`), the admin shell has no published preset today —
you configure Vite directly:

`vite.config.ts`:

```ts
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  build: {
    outDir: 'src/Resources/public/admin', // only this path is published
    emptyOutDir: true,
    lib: {
      entry: 'src/main.ts',
      formats: ['iife'],
      name: 'MyPluginAdmin',              // unique IIFE global per plugin
      fileName: () => 'main.js',
    },
    rollupOptions: {
      external: ['vue'],                  // Vue is resolved from the shell, not bundled
      output: {
        assetFileNames: (info) =>
          info.name?.endsWith('.css') ? 'main.css' : (info.name ?? '[name][extname]'),
      },
    },
  },
})
```

::: warning Keep the output directory
Only `src/Resources/public/admin` is published. If you point `outDir` elsewhere, the
publisher won't pick your bundle up and nothing loads at `/admin`.
:::

> **Status:** There is currently **no** `window.CalloraVue`-style global for the admin shell
> and no published admin build preset (both exist on the surface side). The supported way to
> build admin components today is with the shell's `CalloraAdmin.vue.defineComponent` / `h`
> primitives (render functions), as shown below. A blessed admin Vite preset and a shared-Vue
> global that would let you author single-file `.vue` templates against the shell's Vue are
> planned; until then, mark `vue` external and use the render-function primitives.

## Step 3 — Write the entry module

Everything registers from one `main.ts`, at top level, so the loader attributes each
registration to your plugin. This example uses **all three** mechanisms.

`src/main.ts`:

```ts
// The shell installs this global before mounting. Declare its shape locally so
// TypeScript is happy without a build-time dependency on the shell.
interface HookContext<T> {
  readonly payload: T
  cancel(reason?: string): void
}

interface CalloraAdminGlobal {
  registerExtension(slot: string, component: unknown, order?: number): void
  registerHook<T>(name: string, handler: (ctx: HookContext<T>) => void | Promise<void>, order?: number): void
  registerService<T>(key: string, implementation: T, meta?: { priority?: number }): void
  vue: {
    h: typeof import('vue')['h']
    defineComponent: typeof import('vue')['defineComponent']
  }
}

declare const CalloraAdmin: CalloraAdminGlobal

const { h, defineComponent } = CalloraAdmin.vue

// 1) SLOT — a metrics widget on the dashboard. The dashboard.metrics slot passes
//    { permissions: string[] } as ctx.
const OpenCallsWidget = defineComponent({
  name: 'OpenCallsWidget',
  props: { ctx: { type: Object, default: () => ({}) } },
  setup() {
    return () =>
      h('section', { class: 'acme-widget' }, [
        h('h3', 'Open calls'),
        h('p', 'Live count wired to your backend.'),
      ])
  },
})
CalloraAdmin.registerExtension('dashboard.metrics', OpenCallsWidget, 20)

// 2) HOOK — veto a user save for non-company emails; normalise otherwise.
interface UserDraft { email: string }
CalloraAdmin.registerHook<UserDraft>('users.before-save', (ctx) => {
  const email = ctx.payload.email?.trim() ?? ''
  if (!email.endsWith('@acme.example')) {
    ctx.cancel('Only @acme.example addresses are allowed')
    return
  }
  ctx.payload.email = email.toLowerCase()
})

// 3) SERVICE OVERRIDE — replace the users API with our own backend.
class AcmeUsersApi {
  async list() {
    return (await fetch('/acme-api/users', { credentials: 'include' })).json()
  }
  async save(draft: UserDraft) {
    return (
      await fetch('/acme-api/users', {
        method: 'POST',
        credentials: 'include',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(draft),
      })
    ).json()
  }
  async delete(userId: string) {
    await fetch(`/acme-api/users/${userId}`, { method: 'DELETE', credentials: 'include' })
  }
}
CalloraAdmin.registerService('usersApi', new AcmeUsersApi(), { priority: 10 })
```

::: warning Register synchronously, at top level
The loader attributes registrations to a plugin only while its bundle is *synchronously*
executing. A call deferred via `setTimeout` or a dynamic `import()` runs after that window
closes and is recorded with a `null` owner. Keep all `register*` calls at the top level of
`main.ts`.
:::

## Step 4 — Build

From the bundle directory:

```bash
npm install
npm run build
```

**Expected result:** `src/Resources/public/admin/` now contains `main.js` (and `main.css` if
a component emitted styles). These are the files that ship with the plugin.

::: warning Build before you publish
The host publishes only **built JavaScript**. A bundle with a `main.ts` source but no built
`main.js` is treated as *unbuilt*: the publisher logs a warning and the admin UI never loads
(`src/Core/Infrastructure/Plugins/PluginUiAssetPublisher.cs`). Always build before installing
or shipping.
:::

## Step 5 — Publish and load

You wire nothing up by hand — publication and loading are automatic:

1. **Publish.** When the plugin is active, `PluginUiAssetPublisher` publishes the `admin`
   surface: it copies `src/Resources/public/admin/` to
   `<webroot>/plugin-assets/<pluginId>/app/admin/` and records the entry (and any `main.css`)
   in the UI-asset manifest at `/manifests/plugin-ui-assets.manifest.json` with
   `surface: "admin"`.
2. **Load.** On first load of `/admin`, the shell's `main.ts` runs `loadPluginExtensions()`
   **before mounting**: it installs `window.CalloraAdmin`, fetches the manifest, appends each
   admin style, then injects each admin script sequentially — each attributed to its plugin.
   Your `main.ts` runs, all three registrations fire, and the shell mounts with them present.

Loading is **fault-tolerant**: a missing manifest, an offline fetch, or a broken bundle
leaves the shell running without your plugin rather than crashing it (`loader.ts`). Every
bundle's outcome is recorded as a load result so a silently-dropped UI is diagnosable rather
than invisible.

## Step 6 — See it work

Open `/admin`:

- **Dashboard** — your "Open calls" widget appears in the metrics area (the
  `dashboard.metrics` slot).
- **Users** — saving a user with a non-`@acme.example` email is **blocked** with your reason;
  the users list and detail now go through your `AcmeUsersApi`.

**Expected result:** all three mechanisms are live from the one bundle, no host restart.

::: tip Nothing showing?
Walk the chain outward: is the plugin **active**? Did the build produce `main.js` under
`Resources/public/admin`? Does `/manifests/plugin-ui-assets.manifest.json` list your entry
for `surface: "admin"`? Did you register at **top level** (not in a `setTimeout`)? Each layer
is independent, so the break is usually in exactly one of them.
:::

## The complete picture

```text
src/main.ts  ──build──▶  Resources/public/admin/main.js
                                    │
                          PluginUiAssetPublisher (on activate)
                                    ▼
        /plugin-assets/<id>/app/admin/main.js  +  manifest entry (surface: "admin")
                                    │
              loadPluginExtensions() installs window.CalloraAdmin,
              reads the manifest, injects <script> before mounting
                                    ▼
   main.js runs → registerExtension / registerHook / registerService → shell mounts
```

## Next steps

- Go deeper on each mechanism: **[Slots](./slots)**, **[Hooks](./hooks)**,
  **[Service overrides](./service-overrides)**
- The extension model overview: **[Extending the admin shell](./)**
- Export your extensions from the backend plugin: **[Exporting extensions](/guides/fundamentals/exporting-extensions)**
- The tenant-facing counterpart: **[Building a surface plugin](/guides/surface/building-a-surface-plugin)**
- Slot / hook / service key contracts: **[Extension manifests & contracts](/reference/extension-manifests)**
