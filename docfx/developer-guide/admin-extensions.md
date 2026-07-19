# Admin & Surface Extensions

Callora has two distinct front-end runtimes, and a plugin can extend either or both:

1. The **admin shell** — the operator/backoffice SPA, extended through
   `window.CalloraAdmin` (slots, hooks, service overrides).
2. **Surfaces** — the tenant-facing rendering layer, extended with Vue views through the
   `@callora/surface-sdk`.

Both are colocated Vue 3 SPAs living inside their host modules. (Anything under `apps/` is
**legacy** and is not documented here.)

---

## Admin-shell extensions

The admin shell is a Vue 3 SPA at
`src/Administration/Resources/app/administration`. When a plugin's admin bundle loads, it
sees a global **`window.CalloraAdmin`** installed by the shell's extension loader
(`src/core/extensions/loader.ts`). Registrations must happen **synchronously at bundle
top-level** so the shell can attribute them to your plugin.

```ts
interface CalloraAdminGlobal {
  registerExtension(slot: string, component: Component, order?: number): void
  registerHook<T>(name: string, handler: (ctx: HookContext<T>) => void | Promise<void>, order?: number): void
  registerService<T>(key: string, implementation: T, meta?: { priority?: number }): void
  vue: { h; defineComponent }   // build components without bundling Vue
}
```

### Slots — additive UI

Register a component into a named slot; **every** registered component renders, in ascending
`order`.

```ts
CalloraAdmin.registerExtension(
  'users.detail.fields',
  CalloraAdmin.vue.defineComponent({ /* … */ }),
  20,
)
```

The shell renders slots with the `ExtensionSlot` component
(`src/core/extensions/ExtensionSlot.vue`), which passes an optional `ctx` prop through to
every contributed component:

```vue
<ExtensionSlot name="users.detail.fields" :ctx="user" />
```

### Hooks — before/after, cancelable

Hooks let a plugin observe and **veto** shell actions. Handlers run sequentially in
ascending `order`, share one mutable `payload`, are awaited, and any handler can cancel —
which stops the remaining handlers. The runner (`src/core/extensions/hooks.ts`) returns
`{ canceled, cancelReason }`:

```ts
CalloraAdmin.registerHook<{ pluginId: string }>('plugins.before-activate', (ctx) => {
  if (!isLicensed(ctx.payload.pluginId)) {
    ctx.cancel('not licensed')   // shell aborts the activation
  }
})
```

`before-*` hooks are the cancelable ones (a `cancel()` aborts the pending action);
`after-*` hooks observe a completed action.

### Service overrides — exclusive

Slots are additive; service overrides are **exclusive** — only the highest-priority
implementation wins. A view resolves a service with `useService(key, fallback)`
(`src/core/extensions/services.ts`); a plugin overrides it:

```ts
CalloraAdmin.registerService('usersApi', new MyUsersApi(), { priority: 10 })
```

Conflicts (more than one override for a key) are retained and reportable via
`getServiceConflicts()`, so the shell can show which plugin is active and which are
shadowed.

---

## Surface plugins

Surfaces are the tenant-facing layer. Server-side, the SSR engine renders Nunjucks
templates (ADR-015); client-side, a
colocated Vue 3 runtime at `src/Surface.Rendering/Resources/app/surface` hydrates that
output with plugin views. You build those views against **`@callora/surface-sdk`**
(`custom/surface-sdk` — see its README).

### The typed contract

The SDK is a thin, typed wrapper over the runtime's registry:

```ts
interface SurfaceContext { workspaceKey: string; surfaceKey: string }
interface SurfaceView    { id: string; component: Component; order?: number }

function registerSurfaceView(view: SurfaceView): void
```

`registerSurfaceView` pushes into the runtime's `window.calloraSurface` registry. It is a
**no-op with a warning** (never throws) if the runtime is absent, so a plugin bundle can
never break the shell it loads into.

### One shared Vue: `window.CalloraVue`

The surface runtime owns the single Vue instance and re-exposes it as `window.CalloraVue`.
Plugin bundles keep **Vue external** and resolve it from that global, so every plugin runs
inside the *same* Vue instance and reactive system — no duplicate Vue, no cross-instance
state bugs. The Vite preset wires this for you.

### App mode vs. islands mode

The runtime mounts registered views two ways (`src/.../surface/src/mount.ts`):

- **App mode** — when the SSR output contains `<div id="callora-app">`, the runtime mounts
  one Vue app there and renders **all** registered views (full-page surfaces).
- **Islands mode** — for each `<div data-callora-island="voip.calls">` placeholder in the
  SSR HTML, the runtime mounts a small app that renders **only** the view whose `id`
  matches. This is progressive enhancement of server-rendered content.

The registry is reactive, so a plugin bundle that loads late still triggers a render. Bundles
are fetched by a chain-loader that reads the workspace's UI chain
(`/workspace/public/ui-chain`) and the asset manifest, then injects each plugin's scripts and
styles in chain order — published bundles resolve under
`/plugin-assets/<id>/views/workspace`. Loading is fault-tolerant: a missing manifest, an
offline fetch, or a broken bundle degrades gracefully to the shell without plugins.

### A minimal surface plugin

`main.ts` — a normal `.vue` import resolves `import … from 'vue'` to `CalloraVue`:

```ts
import { registerSurfaceView } from '@callora/surface-sdk'
import CallsPage from './CallsPage.vue'

registerSurfaceView({ id: 'voip.calls', component: CallsPage, order: 10 })
```

`vite.config.ts` — the blessed preset does all the wiring (Vue external → `CalloraVue`,
IIFE `main.js` / `main.css`, output to the published `Resources/public/<surface>`):

```ts
import { defineConfig } from 'vite'
import { calloraSurfacePlugin } from '@callora/surface-sdk/vite-preset'

export default defineConfig(
  calloraSurfacePlugin({
    entry: 'src/Resources/app/workspace/src/main.ts',
    name: 'CalloraVoipWorkspace',   // unique IIFE global per plugin
    surface: 'workspace',
  }),
)
```

The preset's Rollup output is the load-bearing part — it keeps every plugin on the shared
Vue:

```ts
build: {
  lib: { entry, formats: ['iife'], name, fileName: () => 'main.js' },
  rollupOptions: {
    external: ['vue'],
    output: {
      globals: { vue: 'CalloraVue' },        // Vue resolved from window.CalloraVue
      assetFileNames: /* → 'main.css' */,
    },
  },
}
```

Your `CallsPage.vue` receives the `SurfaceContext` (`{ workspaceKey, surfaceKey }`) as a
`context` prop.

---

## Which runtime to use

| You want to… | Use |
| --- | --- |
| Add fields, actions, or pages to the **operator** backoffice | Admin shell — `window.CalloraAdmin` slots |
| Veto or observe an operator action | Admin shell — hooks (`registerHook`) |
| Replace an admin data service | Admin shell — service overrides (`registerService`) |
| Add UI to a **tenant-facing** website / portal | Surface plugin — `@callora/surface-sdk` |
| Enhance server-rendered content in place | Surface plugin — islands mode |

The admin shell's SPA/Vue extension path is intentionally **separate** from the surface SSR
world (ADR-014 §10.3): the admin shell is
decoupled from the surface compiler and evolves independently.
