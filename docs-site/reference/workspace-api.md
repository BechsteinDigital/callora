# Workspace (Public) API

The tenant-facing public routes serve a workspace's runtime context, UI chain,
theme tokens, and server-rendered surface to unauthenticated visitors. This is
the public counterpart to the operator/admin API catalogued in the
[REST API](/reference/rest-api). This page catalogues every public workspace
route plus the server-rendered surface route `GET /surface/render`.

Sources: `src/Workspace/Api/WorkspacePublicEndpoints.cs` and
`src/Surface.Rendering/Api/SurfaceRenderEndpoints.cs`.

## Conventions

- **All routes here are anonymous.** Every endpoint is mapped with
  `.AllowAnonymous()` — no session or bearer token is required. The
  access-policy layer (Public / Authenticated / Mixed) is a later phase.
- **Excluded from OpenAPI.** All routes are `.ExcludeFromDescription()`, so they
  do not appear in the Swagger document; they are consumed by the workspace shell
  and embedded surfaces, not by the operator console.
- **Workspace resolution.** Public routes resolve the target workspace from the
  request host and path (honouring `X-Forwarded-Host` / `X-Forwarded-Uri` behind
  a reverse proxy) via `IWorkspaceManagementStore.ResolveByPublicRouteAsync`. A
  workspace is only visible when it **is active**, its **tenant is active**, and
  (when a default tenant key is configured) it belongs to that tenant. Invisible
  or unresolved workspaces yield `404` or a redirect to `/404`.
- **Default workspace key.** Where a `workspaceKey` query parameter is accepted,
  a blank value falls back to `"default"`.

## Routes at a glance

| Method | Path | Purpose | Auth | Response |
| --- | --- | --- | --- | --- |
| GET | `/workspace/public/resolve` | Report whether the request host/path resolves to a workspace. | Anonymous | JSON `{ resolved, workspaceKey }` |
| GET | `/workspace/public/bootstrap.js` | JavaScript that sets `window.__CALLORA_WORKSPACE_CONTEXT__`. | Anonymous | `application/javascript` (no-store) |
| GET | `/workspace/public/context` | The resolved workspace + public route metadata. | Anonymous | JSON `{ workspace, route }` or `404` |
| GET | `/workspace/public/ui-chain` | The workspace's ordered UI-chain plugin ids. | Anonymous | JSON `{ workspaceKey, chain }` or `404` |
| GET | `/workspace/public/theme` | The workspace's effective theme tokens. | Anonymous | JSON `{ workspaceKey, themePluginId, themeVersion, valuesByKey }` |
| GET | `/surface/render` | Server-render the workspace's own template chain (or the SPA shell). | Anonymous | `text/html` or `404` |

> The same file also maps a few public **redirect** routes on this host —
> `/login`, `/` and the catch-all `/{**path:nonfile}` — which forward the visitor
> to the workspace shell, the admin shell (`/admin/*`), or `/404`. They are part
> of the front-door routing rather than a data API and are not detailed
> individually here.

For building what these routes serve, see the
[Surface guides](/guides/surface/).

---

## GET `/workspace/public/resolve`

Cheap probe: does the incoming request resolve to a visible workspace?

- **Query params.** None. Host/path come from the request (proxy-aware:
  `X-Forwarded-Host`, `X-Forwarded-Uri`; path falls back to `/`).
- **Auth.** Anonymous.
- **Response.** Always `200 OK`:

```json
{ "resolved": true, "workspaceKey": "acme" }
```

`resolved` is `false` and `workspaceKey` is `null` when nothing visible matches.

---

## GET `/workspace/public/bootstrap.js`

Returns a small JavaScript snippet that injects the workspace context onto the
page, for surfaces embedded on a foreign host.

- **Query params.**

  | Name | Required | Meaning |
  | --- | --- | --- |
  | `path` | No | The logical page path to resolve against. If omitted, the path is taken from the `Referer` header, else `/`. |

- **Host.** From `X-Forwarded-Host` or the request host.
- **Auth.** Anonymous.
- **Response.** `Content-Type: application/javascript; charset=utf-8`, sent with
  `Cache-Control: no-store, no-cache, must-revalidate`. Body:

```javascript
window.__CALLORA_WORKSPACE_CONTEXT__ = {
  "workspace": { "key": "acme", "name": "Acme", "type": "base" },
  "route": { "publicBaseUrl": "https://…", "publicPathPrefix": "/acme" }
};
```

When no workspace resolves, a default payload is emitted (`key: "default"`,
`name: "Workspace"`, `type: "base"`, `publicBaseUrl` = the workspace shell base
URL, `publicPathPrefix` = the request path).

---

## GET `/workspace/public/context`

The resolved workspace and its public routing metadata, as JSON.

- **Query params.**

  | Name | Required | Meaning |
  | --- | --- | --- |
  | `path` | No | Page path to resolve against (default `/`, normalized). |

- **Host.** From `X-Forwarded-Host` or the request host.
- **Auth.** Anonymous.
- **Response.** `404 Not Found` when no visible workspace resolves; otherwise
  `200 OK`:

```json
{
  "workspace": { "key": "acme", "name": "Acme", "type": "base" },
  "route": {
    "publicBaseUrl": "https://acme.example.com",
    "publicHost": "acme.example.com",
    "publicPathPrefix": "/"
  }
}
```

---

## GET `/workspace/public/ui-chain`

The ordered list of UI-chain plugin ids for a workspace — resolved by
`WorkspaceUiChainResolver` — used by the shell/loader to load the workspace's
plugin bundles. The primary plugin is `chain[0]`.

- **Query params.**

  | Name | Required | Default | Meaning |
  | --- | --- | --- | --- |
  | `workspaceKey` | No | `default` | Workspace whose chain to resolve. |

- **Auth.** Anonymous. Only workspaces visible in the configured default tenant
  expose their chain.
- **Response.** `404 Not Found` when the workspace is not visible; otherwise
  `200 OK`:

```json
{ "workspaceKey": "acme", "chain": ["acme-theme", "acme-storefront"] }
```

`chain` is an ordered array of plugin-id strings.

---

## GET `/workspace/public/theme`

The workspace's effective theme: the resolved theme plugin, its version, and the
flattened token map — served by `WorkspacePublicThemeResolver`.

- **Query params.**

  | Name | Required | Default | Meaning |
  | --- | --- | --- | --- |
  | `workspaceKey` | No | `default` | Workspace whose theme to resolve. |

- **Auth.** Anonymous.
- **Response.** Always `200 OK`. When no theme is resolved, `themePluginId` and
  `themeVersion` are `null` and `valuesByKey` is an empty object:

```json
{
  "workspaceKey": "acme",
  "themePluginId": "acme-theme",
  "themeVersion": "1.2.0",
  "valuesByKey": { "color.primary": "#3355ff", "radius.md": "8px" }
}
```

::: tip Authenticated variant
An authenticated, permission-gated theme route also exists —
`GET /workspace/themes/effective` (requires `extension.read` and workspace
access) — returning the resolved template records rather than the flattened
token map. See the [REST API](/reference/rest-api). The public `theme` route
above needs no session.
:::

For the token model and `theme.json` shape, see
[Themes and tokens](/guides/surface/themes-and-tokens) and
[Extension manifests](/reference/extension-manifests).

---

## GET `/surface/render`

Server-renders a workspace's public surface (ADR-015 §7). It resolves the request
host/path to a workspace, then server-renders that workspace's own template chain:
when the primary UI-chain plugin publishes a surface entry (`index.njk`), the
entry is rendered through the confined bundle loader with the full plugin chain in
scope (so its Nunjucks `extends`/`block`/`include` resolve). A workspace that
publishes no entry — or a host that does not provide the chain resolver — falls
back to the built-in SPA shell.

- **Query params.** None. Host and path come from the request (`Request.Host.Host`
  and `Request.Path`, default `/`).
- **Auth.** Anonymous. The access-policy layer is a later phase.
- **Resolution & failure.** `404 Not Found` when no active workspace (with an
  active tenant) resolves.
- **Render context.** Built as `SurfaceRenderContext` with `SurfaceKey: "default"`,
  `SurfaceType: "spa"`, `Locale: "de"`, and a `tokens` map that includes
  `themePluginId` when the workspace has one.
- **Resilience.** If the plugin's entry template throws a
  `SurfaceTemplateException`, the failure is logged and the response degrades to
  the SPA shell — a broken plugin template does not take the public surface down.
- **Response.** `200 OK`, `Content-Type: text/html; charset=utf-8` — the rendered
  HTML (either the plugin's surface entry or the SPA shell).

See [SSR templates](/guides/surface/ssr-templates) and
[Building a surface plugin](/guides/surface/building-a-surface-plugin).
