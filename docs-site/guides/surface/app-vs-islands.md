# App mode vs Islands mode

The surface client runtime mounts your Vue views in one of **two modes**, decided entirely
by the shape of the SSR output. Both are driven by the same registry and the same shared
Vue; a given surface uses one or the other. This page covers both in depth, how a view
inherits its context, and why a view registered *after* the DOM mounted still appears.

Everything here is grounded in
`src/Surface.Rendering/Resources/app/surface/src/mount.ts`.

## What you'll learn

- How **app mode** (`#callora-app`) and **islands mode** (`data-callora-island`) differ and
  when to use each
- How `mountSurface` scans for each and mounts your views
- How `resolveSurfaceContext` inherits the workspace/surface context for an island
- Why late-registered views appear without re-scanning the DOM (reactive mounting)

## The two modes at a glance

| | App mode | Islands mode |
| --- | --- | --- |
| SSR marker | one `#callora-app` root | `[data-callora-island="<viewId>"]` placeholders |
| What mounts | **every** registered view, in `order` | **one** matching view per placeholder |
| Rest of page | there is none — Vue owns the document body | static SSR HTML, untouched |
| Emitted by | the built-in `SurfaceShell` | your `.njk` content template |
| Fits | interactive app-surfaces (portal, dialer) | content-surfaces with interactive pockets |

## How `mountSurface` works

`mountSurface` runs both scans; they are independent, so the same runtime handles either
shape without configuration:

```ts
export function mountSurface(registry: SurfaceRegistry, doc: Document = document): void {
  const appRoot = doc.getElementById('callora-app')
  if (appRoot) {
    createApp(App, { context: readSurfaceContext(appRoot), registry }).mount(appRoot)
  }

  const islands = doc.querySelectorAll<HTMLElement>('[data-callora-island]')
  islands.forEach((island) => {
    const viewId = island.dataset.calloraIsland
    if (!viewId) {
      return
    }
    createApp(islandHost(registry, viewId, resolveSurfaceContext(island))).mount(island)
  })
}
```

## App mode

App mode is the default the built-in shell emits. The SSR document carries a single root:

```html
<div id="callora-app" data-workspace="acme" data-surface="portal"></div>
```

`mountSurface` finds `#callora-app` and mounts the runtime's `App.vue` into it. `App.vue`
renders **every** view in the registry, sorted by `order`, each receiving the
`SurfaceContext` as a `context` prop:

```vue
<component
  :is="view.component"
  v-for="view in views"
  :key="view.id"
  :context="context"
/>
```

The whole surface is one Vue app. If no plugin has registered a view, `App.vue` shows a
neutral placeholder — an empty surface is a valid state, not an error.

**Use app mode when** the surface is interactive end-to-end and there is no meaningful
server-rendered content to preserve — a customer portal, an agent console, a dialer. You
write no SSR template; the shell already emits `#callora-app`, and your registered views
fill it.

## Islands mode

Islands mode enhances server-rendered content. Your `.njk` template
(see [SSR Templates](./ssr-templates)) emits real HTML and drops placeholders where
interactivity is needed:

```html
<section class="hero">
  <h1>Welcome to Acme</h1>            <!-- static SSR HTML -->
</section>

<div data-callora-island="my-plugin.booking-widget"></div>  <!-- an island -->

<footer>© Acme</footer>              <!-- static SSR HTML -->
```

For each placeholder, `mountSurface` reads the island id from `data-callora-island` and
mounts a **one-slot host** that renders only the registered view whose `id` matches. The
rest of the page stays exactly as the server rendered it.

**Use islands mode when** the surface is mostly content — a landing page, a marketing
site, a mostly-static portal — that needs interactive pockets. You get a fast, indexable
first paint from SSR, and Vue hydrates only the islands.

::: tip The island id is the view id
`data-callora-island="my-plugin.booking-widget"` mounts the view registered as
`registerSurfaceView({ id: 'my-plugin.booking-widget', … })`. Keep the two in lockstep —
a typo just renders nothing (see reactive mounting below).
:::

## Context inheritance

In app mode, `#callora-app` carries the context directly on its `data-workspace` /
`data-surface` attributes, read by `readSurfaceContext`.

An island usually does **not** carry those attributes itself — it's a bare placeholder
inside content. `resolveSurfaceContext` walks up to the nearest ancestor that does:

```ts
export function resolveSurfaceContext(el: HTMLElement): SurfaceContext {
  const source = el.closest<HTMLElement>('[data-workspace]') ?? el
  return readSurfaceContext(source)
}
```

So in a content template, put `data-workspace` / `data-surface` on a wrapper once, and
every island inside inherits it:

```html
<body data-workspace="acme" data-surface="site">
  ...
  <div data-callora-island="my-plugin.booking-widget"></div>
  ...
</body>
```

Missing attributes fall back to `'default'` (`readSurfaceContext`), so an island never
mounts without a context.

## Reactive late registration

Plugin bundles load **after** `mountSurface` has already run — `main.ts` mounts first,
*then* fires `loadSurfacePlugins`. An island whose view hasn't registered yet must still
light up when its bundle arrives. It does, because the island host looks its view up
reactively:

```ts
function islandHost(registry: SurfaceRegistry, viewId: string, context: SurfaceContext) {
  return defineComponent({
    name: 'CalloraSurfaceIsland',
    setup() {
      const view = computed(() => registry.views.find((candidate) => candidate.id === viewId))
      return () => (view.value ? h(view.value.component, { context }) : null)
    },
  })
}
```

The registry's `views` is a Vue `reactive` array
(`src/Surface.Rendering/Resources/app/surface/src/surface-registry.ts`). When a plugin
calls `registerView`, the `computed` re-evaluates and the island renders — no DOM
re-scan, no manual refresh. The same reactivity backs app mode: `App.vue`'s `views` is a
`computed` over the registry, so a late view simply appears in the list.

::: info Chain order preserved
Bundles are injected with `script.async = false`, so they execute in UI-chain order — a
bundle that builds on an earlier one runs after it
(`src/Surface.Rendering/Resources/app/surface/src/plugin-loader.ts`). Within app mode,
final render order is still governed by each view's `order`.
:::

## Choosing between them

- Interactive everywhere, no server content to keep → **app mode** (nothing to author; the
  shell emits `#callora-app`).
- Server-rendered content with interactive pockets → **islands mode** (author a `.njk`
  template, drop `data-callora-island` placeholders).

A single view can serve both: registered once, it renders inside `#callora-app` in app
mode *or* wherever a matching island appears in islands mode.

## Next steps

- Register the views these modes mount: **[Building a surface plugin](./building-a-surface-plugin)**
- Author the SSR HTML that hosts islands: **[SSR Templates](./ssr-templates)**
- Style views and islands with tokens: **[Themes & Tokens](./themes-and-tokens)**
