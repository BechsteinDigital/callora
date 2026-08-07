# Surfaces

A **surface** is a tenant-facing front-end of a workspace — the public thing an end
user actually visits (Shopware would call it a *sales channel*). One workspace can
expose several surfaces; a surface is roughly a *SalesChannel* on the workspace axis.

Callora renders a surface in two cooperating layers, and a plugin plugs into either
one:

- a **server-side render (SSR) layer** that emits the HTML document, and
- a **client runtime** — a colocated Vue 3 app (not Nuxt) that boots inside that
  document and renders your Vue views.

This guide teaches both layers, how they meet, and how to decide which shape your
plugin should take.

## What you'll learn

- The two layers of a surface — SSR HTML and the client Vue runtime — and how they hand
  off through the `#callora-app` root
- The two client **mount modes**: whole-app vs islands
- The three places a plugin plugs in: a **Vue view**, a **`.njk` template**, or a
  **theme**
- How to decide between an **app-surface** and a **content-surface**
- A learning path through the rest of this guide

## The two layers

### SSR layer — the server renders HTML

The public route `GET /surface/render`
(`src/Surface.Rendering/Api/SurfaceRenderEndpoints.cs`) resolves the request
host/path to a workspace surface and returns an HTML document. What it renders depends
on the workspace's plugin chain:

- **The built-in SurfaceShell.** When no plugin in the chain publishes a template, the
  server emits the minimal `SurfaceShellTemplates.SpaRoot`
  (`src/Surface.Rendering/SurfaceShellTemplates.cs`): a single mount point plus the
  client runtime.

  ```html
  <div id="callora-app" data-workspace="{{ workspace.key }}" data-surface="{{ surface.key }}"></div>
  <script src="/surface-app/surface.js" defer></script>
  ```

  The shell ships **no UI of its own** — it is a neutral scaffold (*Grundgerüst*). Every
  concrete surface comes from a plugin.

- **A full SSR template.** When the chain's primary plugin publishes an entry template
  (`index.njk`), the server renders *that* instead, replacing the shell entirely. This
  is the content-surface path — see [SSR Templates](./ssr-templates).

The renderer is `NunjucksSurfaceRenderer` — Nunjucks templates (native Twig-style
`extends`/`block`/`super`/`include`) executed on a hardened Jint JS sandbox with no CLR
access. Details in [SSR Templates](./ssr-templates).

### Client runtime — one shared Vue

The runtime lives at `src/Surface.Rendering/Resources/app/surface/` and builds to
`/surface-app/surface.js` + `surface.css`. Its `main.ts`:

1. exposes its Vue instance as `window.CalloraVue` (so plugin bundles keep Vue external
   and share one instance),
2. creates the registry `window.calloraSurface`,
3. mounts whatever shape the SSR output rendered, then
4. loads the workspace's plugin bundles from the UI chain.

```ts
window.CalloraVue = Vue
const registry = window.calloraSurface ?? createSurfaceRegistry()
window.calloraSurface = registry
mountSurface(registry)
// then: void loadSurfacePlugins(resolveSurfaceContext(contextRoot))
```

## The two mount modes

`mount.ts` (`src/Surface.Rendering/Resources/app/surface/src/mount.ts`) supports two
independent shapes — a surface uses one **or** the other:

- **App mode** — a single `#callora-app` root. The whole surface is one Vue app that
  renders every registered view. This is what the built-in shell emits, for interactive
  *app-surfaces*.
- **Islands mode** — `<div data-callora-island="<viewId>">` placeholders embedded in
  server-rendered content. Vue mounts only the matching registered view into each
  placeholder, leaving the rest of the page as static SSR HTML — for *content-surfaces*
  with progressive enhancement.

Both are driven by the same registry and the same shared Vue. Full treatment in
[App vs Islands](./app-vs-islands).

## Where a plugin plugs in

A surface plugin contributes one of three things:

| Contribution | Layer | You ship | Learn in |
| --- | --- | --- | --- |
| A **Vue view** | Client | An IIFE bundle under `Resources/public/<surface>` that calls `registerSurfaceView` | [Building a surface plugin](./building-a-surface-plugin) |
| A **block** | Client | The same bundle, calling `registerBlock` — a view plus the metadata an editor needs to offer and configure it | [Building a surface plugin](./building-a-surface-plugin) |
| A **`.njk` template** | SSR | Nunjucks views under `Resources/views/surface/` (entry `index.njk`) | [SSR Templates](./ssr-templates) |
| A **theme** | Both | A `theme.json` declaring `--cal-*` tokens + settings | [Themes & Tokens](./themes-and-tokens) |

These are not mutually exclusive — a content-surface plugin commonly ships a `.njk`
template *and* island Vue views *and* a theme.

::: info The runtime is empty by design
The surface runtime registers **no views of its own**. An unconfigured surface renders a
neutral "no surface registered" placeholder. That is a valid state, not an error — like a
shop framework with no shop installed yet.
:::

### Showing surface blocks somewhere that is not a surface

An editor's canvas has to render the real block components, not an approximation of them,
or the preview drifts from the result. `@callora/surface` exports the loading itself for
that case:

```ts
import { loadSurfaceBundles } from '@callora/surface'

const { registry, results, styles } = await loadSurfaceBundles({
  workspaceKey: 'acme',
  surfaceKey: 'portal',
  injectStyles: false, // the host scopes them itself — see below
})
```

It resolves the workspace's UI chain, injects the bundles in chain order, and creates the
registry **before** the first bundle runs — a bundle that executes without one registers
into nothing, warns to the console, and leaves an empty canvas with no error to find.

`injectStyles: false` matters outside a surface. A surface stylesheet claims names like
`.cal-header` that mean something on both sides, so injecting it into an admin document
would restyle the shell around the canvas. The URLs come back either way, so the host
fetches their text and scopes it (`@scope`, rewriting `:root`/`html`/`body` onto the scope
root — `:root` is the document element and escapes every `@scope`).

The package also exports the neutral base tokens as text, for the same host:

```ts
import { surfaceBaseTokens } from '@callora/surface'
```

On a surface the runtime loads `tokens.scss` itself. In an editor canvas nobody does, so a
block reading `var(--cal-color-fg)` would fall back to nothing — a preview that looks wrong
without anything appearing broken. Text rather than a file because the host has to scope it
before applying it, and because a `?inline` import across a package boundary would force
sass and an out-of-project file read on every consumer.

## App-surface or content-surface?

Pick the shape from how the surface behaves, not from a technology preference.

**Choose an app-surface (app mode) when** the surface is primarily interactive and
client-driven — a portal, a dialer, an agent console. The server renders only the shell;
your whole UI is Vue. Chosen automatically: the built-in shell emits `#callora-app`, and
your registered views render inside it.

**Choose a content-surface (islands mode) when** the surface is mostly server-rendered
content — a landing page, a marketing site, a mostly-static portal — that needs
interactive pockets. You ship a `.njk` template for the page and drop
`data-callora-island` placeholders where interactivity is needed. The server sends real
HTML (fast first paint, indexable); Vue hydrates only the islands.

::: tip Rule of thumb
Interactivity everywhere → **app-surface**. Content with interactive pockets →
**content-surface**. When in doubt, start with an app-surface — it needs no SSR template
and is the default the shell already emits.
:::

## Learning path

1. **[Building a surface plugin](./building-a-surface-plugin)** — the flagship tutorial:
   scaffold a Vue bundle with `@callora/surface`, register a view, build, ship the assets,
   and see it render.
2. **[App vs Islands](./app-vs-islands)** — the two mount modes in depth, context
   inheritance, and reactive late registration.
3. **[SSR Templates](./ssr-templates)** — server-rendered `.njk` templates, the sandbox,
   and template inheritance.
4. **[Themes & Tokens](./themes-and-tokens)** — the `--cal-*` token cascade and
   `theme.json`.
5. **[Media & Assets](./media-and-assets)** — how your front-end assets are published and
   served, plus the media library.

## Next steps

- Start building: **[Building a surface plugin](./building-a-surface-plugin)**
- The backend side of a plugin: **[Build your first Callora plugin](/guides/getting-started/your-first-plugin)**
- How exports (including UI assets) leave a plugin: **[Exporting extensions](/guides/fundamentals/exporting-extensions)**
- The big picture: **[Architecture](/concepts/architecture)**
