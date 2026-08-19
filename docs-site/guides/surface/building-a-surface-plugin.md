# Building a surface plugin

In this tutorial you'll build a real surface front-end from nothing: scaffold a Vue
bundle with `@callora/surface`, register a **surface view**, build it to the plugin's
published assets folder, and watch the surface runtime load and render it live — no host
restart. The view is minimal on purpose (a single greeting page reading its
`SurfaceContext`), but every step is the one you'd use for a production surface.

This is the front-end companion to
[Build your first Callora plugin](/guides/getting-started/your-first-plugin), which
covers the backend (the C# plugin, its entry contract, and installation).

## What you'll learn

- How to lay out a surface bundle inside a plugin (`src/Resources/app/<surface>` source →
  `src/Resources/public/<surface>` deliverable)
- How to configure the build with the blessed `calloraSurfacePlugin` Vite preset
- How to register a view with `registerSurfaceView` and receive the `SurfaceContext`
- How the built bundle is **published** to `/plugin-assets/…` and **loaded** through the
  workspace UI chain
- How to see your view render on the surface

::: tip Prerequisites
You'll need:

- **Node 20+** and a package manager (`npm`/`pnpm`) — the surface front-end is a Vite/Vue
  build, independent of the .NET backend build.
- **A Callora plugin** to host the front-end. Any plugin works; if you don't have one,
  scaffold one first with **[Build your first Callora plugin](/guides/getting-started/your-first-plugin)**.
- **A running Callora host** in development, so you can install/activate the plugin and
  open its surface.
- **The `@callora/surface` package** (Apache-2.0) — the typed contract plus the Vite
  preset you compile against. It is the surface **runtime itself**: the contract is not
  restated in a second package, so there is nothing that can drift from what actually
  runs. Install it from npm:

  ```bash
  npm install @callora/surface
  ```

:::

## The mental model

A surface plugin ships **one self-registering IIFE bundle** — `main.js` plus an optional
`main.css` — under `src/Resources/public/<surface>`. Vue is kept **external** and resolved
at runtime from the runtime's shared `window.CalloraVue`, so every plugin runs inside the
*same* Vue instance instead of shipping its own.

The default `<surface>` segment is `surface`. The build outputs to
`src/Resources/public/surface`; on publish the host copies that to
`/plugin-assets/<pluginId>/app/surface/`. The client loader finds it via the manifest
and injects it in chain order. (Sources under `app/` stay with the vendor; only the built
`Resources/public/<surface>` deliverable ships — Shopware-analog.)

## Step 1 — Lay out the bundle

Inside your plugin, create the surface source tree. The build config
(`package.json` + `vite.config.ts`) sits at the **plugin root**, next to your
`registry.json` and (if you have one) the `.csproj` — the same layout the shipped
Communication plugin uses for its own surface bundle:

```text
my-plugin/                       # plugin root — also holds registry.json / .csproj
├── package.json                 # the surface bundle's build config
├── vite.config.ts
└── src/
    └── Resources/
        ├── app/surface/src/   # source (stays with the vendor)
        │   ├── main.ts
        │   └── GreetingPage.vue
        └── public/surface/    # build output — the only thing that ships
```

`package.json` for the bundle (at the plugin root):

```json
{
  "name": "my-plugin-workspace-surface",
  "private": true,
  "type": "module",
  "scripts": {
    "build": "vite build"
  },
  "dependencies": {
    "@callora/surface": "^0.9.0"
  },
  "devDependencies": {
    "@vitejs/plugin-vue": "^5.1.0",
    "vite": "^6.0.0",
    "vue": "^3.5.0"
  }
}
```

::: info Vue is a dev dependency, not a runtime one
You need Vue to *build and type-check*, but the preset marks it external, so it is never
bundled. At runtime the component's `import ... from 'vue'` resolves to
`window.CalloraVue`.
:::

::: info Working inside the Callora repository
The published package ships `dist-lib/` prebuilt, so nothing has to be built first.

If you are developing *against a checkout* rather than a release — changing the runtime
and the plugin together — a `file:` dependency links the source directory instead. That
path resolves to `dist-lib/`, which is gitignored, so run `npm run build:lib` in
`src/Surface.Rendering/Resources/app/surface/` first. Skipping it does not fail the
install: npm links the directory happily, and the *build* then fails with
`ERR_MODULE_NOT_FOUND` for the preset.

Prefer the published package where you can. A linked directory exposes files the `files`
list would never ship, and that difference surfaces after you publish, not before.
:::

## Step 2 — Configure the build

The SDK ships a blessed Vite preset, `calloraSurfacePlugin`, that sets every option a
surface bundle needs: Vue external, an IIFE build with fixed `main.js`/`main.css` names,
and output to `src/Resources/public/<surface>`.

`vite.config.ts`:

```ts
import { calloraSurfacePlugin } from '@callora/surface/vite-preset'

export default calloraSurfacePlugin({
  // Paths are relative to the plugin root (where this vite.config.ts lives).
  entry: 'src/Resources/app/surface/src/main.ts',
  name: 'MyPluginWorkspaceSurface', // must be globally unique per plugin
})
```

The preset options (`src/Surface.Rendering/Resources/app/surface/src/public/vite-preset.ts`):

| Option | Default | Meaning |
| --- | --- | --- |
| `entry` | — | Entry module of the bundle (required) |
| `name` | — | Global name of the IIFE bundle; unique per plugin (required) |
| `surface` | `'surface'` | Which surface the bundle targets; also the output-dir segment |
| `outDir` | `src/Resources/public/<surface>` | Build output directory |

::: warning Keep the default output directory
Only `src/Resources/public/<surface>` is published. If you override `outDir` to something
outside that path, the publisher will not pick your bundle up and the surface will render
empty.
:::

## Step 3 — Write the view component

The component receives two props (`SurfaceViewProps`): the `SurfaceContext` as `context`
— `{ workspaceKey, surfaceKey, caller }` — and the island's instance parameters as
`params`. `params` is what the SSR template passed at the slot's call site, so an
embedded view can point at a concrete lead or room instead of deriving everything from
the URL; in app mode it is empty
(`src/Surface.Rendering/Resources/app/surface/src/public/index.ts`).

`src/GreetingPage.vue`:

```vue
<script setup lang="ts">
import type { SurfaceContext } from '@callora/surface'

const props = defineProps<{ context: SurfaceContext }>()
</script>

<template>
  <main class="greeting">
    <h1>Hello from a surface plugin</h1>
    <p>
      Workspace <code>{{ props.context.workspaceKey }}</code>,
      surface <code>{{ props.context.surfaceKey }}</code>.
    </p>
  </main>
</template>

<style scoped>
.greeting {
  padding: var(--cal-space-4, 1rem);
  font-family: var(--cal-font-sans, system-ui, sans-serif);
  color: var(--cal-color-fg, #1a1a1a);
}
</style>
```

The `--cal-*` custom properties come from the surface's theme tokens — see
[Themes & Tokens](./themes-and-tokens). The fallbacks keep the component readable before a
theme is assigned.

## Step 4 — Register the view

`registerSurfaceView` docks your component into the runtime's registry. The entry module
runs it at load time:

`src/main.ts`:

```ts
import { registerSurfaceView } from '@callora/surface'
import GreetingPage from './GreetingPage.vue'

registerSurfaceView({
  id: 'my-plugin.greeting', // stable, unique; also the island id
  component: GreetingPage,
  order: 10,                // optional — ascending render order in app mode
})
```

`SurfaceView` is `{ id, component, order?, surfaceKeys? }`:

- `id` — a stable, unique id. It's also the value a `data-callora-island` placeholder uses
  to mount this view (see [App vs Islands](./app-vs-islands)). A second registration with
  the same id is ignored.
- `component` — your Vue component; it receives `context` and `params` (see above).
- `order` — ascending render order in app mode; unset sorts as `0`.
- `surfaceKeys` — an allowlist of surfaces this view appears on. Omit it and the view is
  workspace-wide.

::: info It never breaks the shell
If the runtime is somehow absent when your bundle runs, `registerSurfaceView` is a no-op
with a `console.warn` — it never throws. A broken or late plugin leaves the surface
degraded, never crashed (`src/Surface.Rendering/Resources/app/surface/src/public/index.ts`).
:::

### Register a block if editors should place it

A **view** is something a developer places, by writing the island into a template. A
**block** is the same component with the metadata an editor needs to offer it: a label, a
category, and the controls that generate its configuration panel.

```ts
import { registerBlock } from '@callora/surface'
import GreetingPage from './GreetingPage.vue'

registerBlock({
  id: 'my-plugin.greeting', // same id space as a view — and the island attribute
  label: 'Greeting',
  category: 'content',
  component: GreetingPage,
  controls: {
    title: { type: 'text', label: 'Headline', default: 'Hello' },
  },
})
```

A block is not a second kind of thing: registering one registers its view too, under the
same id. So there is one identity for "what the editor placed" and "what the server
rendered", instead of two registries to keep in step.

The control types that shape **appearance** — `colorToken`, `spacingToken`, `typeToken`,
`variant` — are closed, and that is what lets a composed page still look like the product:
each one picks a `--cal-*` role, never a free value. Content and source types are open; a
plugin can contribute its own with `registerControlType`.

## Step 5 — Build

From the bundle directory:

```bash
npm install
npm run build
```

**Expected result:** `src/Resources/public/surface/` now contains `main.js` (and
`main.css` if the component emitted styles). These are the files that ship with the
plugin.

::: warning Build before you publish
The host publishes only built JavaScript — a bundle with a `main.ts` source but no built
`main.js` is treated as *unbuilt*: the publisher logs a warning and the UI never loads
(`src/Core/Infrastructure/Plugins/PluginUiAssetPublisher.cs`). Always run the build before
installing or shipping.
:::

## Step 6 — Publish and load

You don't wire anything up manually — publication and loading are automatic:

1. **Publish.** When the plugin is active, `PluginUiAssetPublisher` copies
   `src/Resources/public/surface/` to
   `<webroot>/plugin-assets/<pluginId>/app/surface/` and records the entry (and any
   `main.css`) in the UI-asset manifest, served at
   `/manifests/plugin-ui-assets.manifest.json`.
2. **Chain.** Add the plugin to the workspace's UI chain (the ordered list of plugin ids
   for the workspace), exposed at `/workspace/public/ui-chain?workspaceKey=<key>`.
3. **Load.** On the surface, the runtime's `plugin-loader.ts` reads that chain and the
   manifest, then injects each plugin's `main.js`/`main.css` in chain order. Your
   `main.ts` runs, `registerSurfaceView` fires, and the reactive mount renders your view.

Loading runs **after** mounting and every failure is tolerated: a missing chain/manifest
or a broken bundle leaves the surface empty but never breaks the shell
(`src/Surface.Rendering/Resources/app/surface/src/plugin-loader.ts`).

## Step 7 — See it render

Open the surface for the workspace (its public route resolves to `GET /surface/render`).
The built-in shell emits `#callora-app`; the runtime boots, loads your bundle, and renders
`GreetingPage`.

**Expected result:** instead of the neutral "no surface registered" placeholder, you see
your greeting, with the workspace and surface keys filled in from the `SurfaceContext`.

::: tip Nothing showing?
Walk the chain outward: is the plugin **active**? Did the build produce `main.js`? Does
`/manifests/plugin-ui-assets.manifest.json` list your entry for `surface: "surface"`?
Is the plugin id present in `/workspace/public/ui-chain`? Each layer is independent, so
the break is usually in exactly one of them.
:::

## The complete picture

```text
src/main.ts  ──build──▶  Resources/public/surface/main.js
                                    │
                          PluginUiAssetPublisher (on activate)
                                    ▼
        /plugin-assets/<id>/app/surface/main.js  +  manifest entry
                                    │
              plugin-loader.ts reads chain + manifest, injects <script>
                                    ▼
     main.js runs → registerSurfaceView → runtime renders your Vue view
```

## Next steps

- Turn this app-view into an SSR island: **[App vs Islands](./app-vs-islands)**
- Ship a full server-rendered page: **[SSR Templates](./ssr-templates)**
- Style it with tokens: **[Themes & Tokens](./themes-and-tokens)**
- Publishing internals & the media library: **[Media & Assets](./media-and-assets)**
- The backend half of the plugin: **[Build your first Callora plugin](/guides/getting-started/your-first-plugin)**
