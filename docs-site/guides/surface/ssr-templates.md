# SSR templates

A surface can be **server-rendered**: instead of the built-in shell, the host renders a
plugin's own `.njk` templates to HTML and returns them. This gives you a full page (fast
first paint, indexable content) that can still host interactive Vue islands. This page
covers the template engine, its sandbox, template inheritance, the bundle layout, and how
`GET /surface/render` resolves and renders your entry.

Grounded in `src/Surface.Rendering/` — `NunjucksSurfaceRenderer`,
`PublishedSurfaceTemplateBundles`, `BundleFileLoader`, `SurfaceRenderEndpoints`,
`SurfaceShellTemplates`.

## What you'll learn

- The Nunjucks-on-Jint rendering engine and its hardened sandbox
- Template inheritance: `extends`, `block`, `super()`, and `include`
- The bundle layout (`Resources/views/surface/`, entry `index.njk`)
- How `GET /surface/render` resolves a workspace's chain and renders its entry
- The context values a template may read
- How the SPA shell fits in as a fallback

::: tip Prerequisites
A working plugin (see [Building a surface plugin](./building-a-surface-plugin)) and a
workspace whose UI chain lists your plugin **first** — the entry template belongs to the
chain's *primary* plugin.
:::

## The engine: Nunjucks on a hardened Jint sandbox

`NunjucksSurfaceRenderer` (`src/Surface.Rendering/Rendering/NunjucksSurfaceRenderer.cs`)
runs the bundled Nunjucks engine — which has native Twig-style inheritance — on the Jint
JavaScript interpreter, in a deliberately locked-down sandbox:

- **No CLR access.** Jint is created without `AllowClr()`, so template JS can never reach a
  .NET type or reflection surface.
- **Fresh engine per render.** Each render gets a new engine; templates cannot contaminate
  one another.
- **JSON-only context.** Only an allowlisted set of string values is serialized to JSON and
  injected — never a .NET object graph.
- **DoS bounds.** A 2-second wall-clock timeout, a 32 MB memory limit, a recursion limit of
  64, a 2,000,000-statement cap, and a 512 KB max output size. Exceeding any of them raises
  a `SurfaceTemplateException`.
- **Autoescape on.** The environment renders with `autoescape: true` and
  `throwOnUndefined: false`.

::: warning Templates are code — treat the trust boundary seriously
The sandbox is defence in depth under the curated / self-hosted trust model. A template is
still executable JavaScript running in your process. Only render `.njk` bundles you trust,
and keep the plugin-signing / provenance checks in place before rendering third-party
templates.
:::

## Template inheritance

Because it's real Nunjucks, you get full Twig-style inheritance. A base layout declares
blocks; child templates extend it and fill or extend those blocks.

`views/surface/base.njk`:

```html
<!doctype html>
<html lang="{{ locale }}">
  <head>
    <meta charset="utf-8" />
    <title>{% block title %}{{ workspace.key }}{% endblock %}</title>
  </head>
  <body data-workspace="{{ workspace.key }}" data-surface="{{ surface.key }}">
    {% block content %}{% endblock %}
    {% include "partials/footer.njk" %}
  </body>
</html>
```

`views/surface/index.njk` (the entry):

```html
{% extends "base.njk" %}

{% block title %}{{ super() }} — Home{% endblock %}

{% block content %}
  <section class="hero"><h1>Welcome to {{ workspace.key }}</h1></section>

  <!-- an interactive island; the runtime mounts the matching registered view -->
  <div data-callora-island="my-plugin.booking-widget"></div>
{% endblock %}
```

- `{% extends "base.njk" %}` — inherit a layout.
- `{% block name %}…{% endblock %}` — declare/override a region.
- `{{ super() }}` — render the parent block's content inside the override.
- `{% include "partials/footer.njk" %}` — inline another template.

### How names resolve — and cross-bundle references

Template names are resolved through `BundleFileLoader`
(`src/Surface.Rendering/Rendering/BundleFileLoader.cs`):

- A **plain relative name** (`"base.njk"`, `"partials/footer.njk"`) resolves against the
  **primary bundle** — the first plugin in the surface's chain, i.e. the rendering
  plugin's own `views/surface` root.
- A **cross-bundle name** `@<pluginId>/path` resolves against another plugin *in the chain*
  — e.g. `{% extends "@acme.base-theme/views/surface/base.njk" %}` to inherit from a base
  theme plugin.

Every resolved path is canonicalised and confined under its bundle root; `../`, absolute
paths, out-of-scope bundles, and missing files all resolve to a template error rather than
escaping the root.

::: info Wire the interactive parts as islands
An SSR page becomes interactive by embedding `data-callora-island` placeholders. The
client runtime mounts the matching registered Vue view into each — see
[App vs Islands](./app-vs-islands). Put `data-workspace`/`data-surface` on a wrapper (as in
`base.njk` above) so every island inherits the context.
:::

## Bundle layout

An SSR template bundle lives under the plugin's `Resources/views/surface/`:

```text
my-plugin/
└── src/
    └── Resources/
        └── views/
            └── workspace/
                ├── index.njk        # the entry (or main.njk)
                ├── base.njk
                └── partials/
                    └── footer.njk
```

On activation the publisher copies this tree to
`<webroot>/plugin-assets/<pluginId>/views/surface/`
(`PluginUiAssetPublisher`), and `PublishedSurfaceTemplateBundles` maps a plugin id back to
that published root for the renderer.

- The **entry** file is `index.njk`, or `main.njk` as a fallback
  (`PublishedSurfaceTemplateBundles.EntryCandidates`). `.njk` is the engine's native
  extension.
- `extends`/`include` targets are ordinary `.njk` files anywhere under that root (or under
  another in-chain bundle via `@id/…`).

## How `GET /surface/render` renders your entry

The public route lives in `SurfaceRenderEndpoints`
(`src/Surface.Rendering/Api/SurfaceRenderEndpoints.cs`). It:

1. **Resolves the workspace** from the request host + path; a missing, inactive, or
   inactive-tenant workspace returns `404`.
2. **Enforces the access policy.** A `Public` workspace (the default) renders anonymously.
   An `Authenticated` workspace served to a caller who is not logged in is **redirected**
   (`302`) to `/login?workspaceKey=…&returnUrl=…` instead of being handed the shell — the
   server is the authoritative boundary (see [Access policy](#access-policy) below).
3. **Resolves the UI chain** for that workspace (`WorkspaceUiChainResolver`).
4. **Reads the primary plugin's entry.** If `chain[0]` publishes an `index.njk`/`main.njk`
   (via `PublishedSurfaceTemplateBundles.TryReadEntryTemplate`), the renderer renders it
   with the **full chain** in scope — so relative `extends`/`include` resolve against the
   primary plugin, and `@id/…` against the rest of the chain.
5. **Falls back to the SPA shell** when the chain is empty, publishes no entry, or the
   entry render throws — the failure is logged and the surface degrades to the built-in
   shell instead of erroring out.

```csharp
if (chain.Count > 0 && bundles.TryReadEntryTemplate(chain[0]) is { } entryTemplate)
{
    try { return renderer.Render(entryTemplate, context, chain); }
    catch (SurfaceTemplateException ex) { /* log, fall through to the SPA shell */ }
}
return renderer.Render(SurfaceShellTemplates.SpaRoot, context);
```

::: warning The entry is the primary plugin's — chain order matters
The entry template always comes from `chain[0]`. If your template plugin isn't first in the
workspace's UI chain, its `index.njk` won't be the page entry (though it can still be
referenced cross-bundle as `@your-plugin/…`). Order the chain so the page-owning plugin
leads.
:::

## Access policy

A workspace carries a **surface access policy** — `Public` or `Authenticated`
(`SurfaceAccessPolicy`, default `Public`). It is the server-side boundary for a surface;
client-side UI hiding is never a substitute.

- **`Public`** — the surface renders anonymously (the historical behaviour). Its UI chain
  is also served anonymously at `/workspace/public/ui-chain`.
- **`Authenticated`** — an anonymous caller is turned away: `GET /surface/render` redirects
  to `/login`, and `GET /workspace/public/ui-chain` returns `404` (indistinguishable from a
  non-existent workspace, so the plugin inventory can't be enumerated). A logged-in caller
  is served normally.

Operators set it with `PUT /api/workspaces/{workspaceKey}/surface-access-policy`
(body `{ "policy": "Authenticated" }`, `workspace.update` permission). It is a
workspace-level v1; distinct per-surface policies arrive with per-surface route resolution.

::: info UI hiding is not a security boundary
The access policy is enforced on the server for every request. A plugin that merely omits a
control from its UI does not restrict access — anything sensitive must be gated by a real
server-side permission check, not by what the surface chooses to render.
:::

## The context a template can read

Only the allowlisted values in `SurfaceRenderContext`
(`src/Surface.Rendering/SurfaceRenderContext.cs`) reach a template, serialized as JSON
(`NunjucksSurfaceRenderer.SerializeContext`):

| Template variable | Source |
| --- | --- |
| `workspace.key` | `WorkspaceKey` |
| `surface.key`, `surface.type` | `SurfaceKey`, `SurfaceType` |
| `tenant.key` | `TenantKey` |
| `locale` | `Locale` |
| `tokens` | `Tokens` (string→string): the effective, secret-filtered theme setting values plus the reserved `themePluginId`/`themeVersion` — bind them onto `--cal-*` yourself |

No other host state is visible. Richer profile/identity context is a later phase.

## The SPA shell (the fallback)

When no plugin publishes an entry, the renderer renders `SurfaceShellTemplates.SpaRoot`
(`src/Surface.Rendering/SurfaceShellTemplates.cs`) — itself a tiny `.njk` document. It's a
single `#callora-app` mount point plus the client runtime, which is exactly the app-mode
path from [App vs Islands](./app-vs-islands). So the two paths meet: publish an entry to
own the whole page (content-surface), or publish none and let the shell boot the Vue
runtime (app-surface).

## Next steps

- Mount interactive views into your SSR page: **[App vs Islands](./app-vs-islands)**
- Build the island views themselves: **[Building a surface plugin](./building-a-surface-plugin)**
- Feed tokens into templates and CSS: **[Themes & Tokens](./themes-and-tokens)**
- The `theme.json` template-definition manifest: **[Extension manifests](/reference/extension-manifests)**
- The render endpoint in the API reference: **[REST API](/reference/rest-api)**
