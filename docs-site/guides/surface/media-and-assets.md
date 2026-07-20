# Media & assets

Two kinds of files matter to a surface: your plugin's **front-end assets** (the built
`main.js`/`main.css` and `.njk` templates that make up the surface) and **workspace media**
(uploaded files — images, audio — your plugin reads at runtime). This page covers how each
is published and served, and the manifest that ties the front-end assets together.

Grounded in `src/Core/Infrastructure/Plugins/PluginUiAssetPublisher.cs`, the manifest
endpoint (`src/Administration/Api/PluginAssetEndpoints.cs`), the client loader
(`plugin-loader.ts`), and the media contracts/endpoints
(`src/Core/Application/Media/Contracts/IMediaLibrary.cs`,
`src/Administration/Api/MediaEndpoints.cs`).

## What you'll learn

- How `PluginUiAssetPublisher` publishes your surface bundle and `.njk` templates
- The paths your assets end up at, and the `src/` unwrapping rule
- The UI-asset manifest — what's in it and where it's served
- How the client loader turns the manifest + UI chain into injected `<script>`/`<link>`
  tags
- The media library (`IMediaLibrary`) and the `/api/media` endpoints

## Front-end asset publishing

When a plugin is **active**, `PluginUiAssetPublisher.PublishAllAsync` copies its front-end
deliverables into the web root under `plugin-assets/` and records them in a manifest. It
publishes two kinds of tree per plugin.

### 1. Compiled surface bundles → `app/<surface>`

For each surface (`admin` and `workspace`), the publisher looks for the plugin's built
deliverable and copies it to `plugin-assets/<pluginId>/app/<surface>/`:

```
src/Resources/public/workspace/   ──▶   /plugin-assets/<pluginId>/app/workspace/
```

It resolves the source directory in this preference order
(`ResolveSurfaceSourceDirectory`): `src/Resources/public/<surface>`, then
`public/<surface>`, then the `app/` fallbacks. The **entry** is the first of
`main.js`/`main.mjs`/`index.js`/… that exists; a matching `main.css`/`style.css`/
`styles.css` is recorded as a style entry.

::: warning Ship built JavaScript, not TypeScript
Only built `.js`/`.mjs` files are valid entries. A source directory that has a `main.ts`
but no built `main.js` is treated as an **unbuilt plugin**: the publisher logs a warning
and records no entry, so the UI never loads. Always build the bundle
(see [Building a surface plugin](./building-a-surface-plugin)) before activating.
:::

### 2. Workspace SSR templates → `views/workspace`

The plugin's `.njk` template tree is copied to
`plugin-assets/<pluginId>/views/workspace/`:

```
src/Resources/views/workspace/   ──▶   /plugin-assets/<pluginId>/views/workspace/
```

This is the root `PublishedSurfaceTemplateBundles` maps a plugin id back to when the SSR
renderer resolves `extends`/`include` and reads the entry template
(see [SSR Templates](./ssr-templates)).

### The `src/` unwrapping rule

The manifest references only *final* paths (ADR-011). If a bundle's entry sits in a `src/`
wrapper (`src/main.js`), the publisher flattens it on copy — `src/main.js` becomes
`main.js` at the published root — so the manifest path has no `src/` segment.

### Path confinement and atomic swap

Plugin ids come from on-disk `registry.json` files, so the publisher hardens against a
crafted id: every target is canonicalised and must stay under the `plugin-assets` root, or
the publish is skipped. The whole set is built into a staging directory and swapped in with
directory renames, so a client never sees a manifest that references not-yet-copied assets,
and a crash mid-build leaves the previous publish intact.

## The UI-asset manifest

The publisher writes a manifest listing every entry, style, and workspace template. It's
served at:

```
GET /manifests/plugin-ui-assets.manifest.json
```

`PluginAssetEndpoints` (`src/Administration/Api/PluginAssetEndpoints.cs`) maps that public
route to the on-disk manifest at `plugin-assets/.build/ui-assets.manifest.json`.

::: info Route name vs on-disk name
The **served route** is `/manifests/plugin-ui-assets.manifest.json` (the client loader's
default). The **file on disk** is `ui-assets.manifest.json` under `plugin-assets/.build/`.
The endpoint bridges the two; use the route, not the disk path.
:::

Shape (fields per `plugin-loader.ts`):

```json
{
  "generatedAtUtc": "2026-07-20T…Z",
  "entries": [
    { "pluginId": "my-plugin", "surface": "workspace", "entryPath": "my-plugin/app/workspace/main.js" }
  ],
  "styleEntries": [
    { "pluginId": "my-plugin", "surface": "workspace", "stylePath": "my-plugin/app/workspace/main.css" }
  ],
  "workspaceTemplates": [
    { "pluginId": "my-plugin", "templatePath": "my-plugin/views/workspace/index.njk" }
  ]
}
```

## How the client loads assets

`plugin-loader.ts` (`src/Surface.Rendering/Resources/app/surface/src/plugin-loader.ts`)
turns the manifest and the workspace's UI chain into injected tags:

1. Fetch the ordered UI chain: `GET /workspace/public/ui-chain?workspaceKey=<key>`.
2. Fetch the manifest: `GET /manifests/plugin-ui-assets.manifest.json`.
3. `resolveSurfaceAssets` filters entries to the requested `surface` **and** to plugins in
   the chain, orders them by chain position, and builds absolute URLs under `/plugin-assets`.
4. `injectSurfaceAssets` appends `<link>`/`<script>` tags to `<head>` — idempotent (a URL
   already present is skipped) and order-preserving (`script.async = false`, so bundles run
   in chain order).

Defence in depth on the client, too: an asset path is rejected if it carries a scheme, is
absolute/protocol-relative, or contains a `..` segment — a bundle src can never point off
the `plugin-assets` root. Every failure (missing chain/manifest, offline server, malformed
response) is tolerated: the surface renders whatever registered, never crashing.

## The media library

Uploaded workspace files — a call announcement, a logo — are the **media library**, distinct
from front-end assets. Plugins get **read** access through `IMediaLibrary`
(`src/Core/Application/Media/Contracts/IMediaLibrary.cs`):

```csharp
public interface IMediaLibrary
{
    Task<IReadOnlyList<MediaAssetInfo>> ListAsync(
        string workspaceKey, string? folder = null, CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(Guid mediaId, CancellationToken cancellationToken = default);
}
```

`MediaAssetInfo` carries the metadata — `Id`, `WorkspaceKey`, `FileName`, `ContentType`
(e.g. `"audio/wav"`), `SizeBytes`, `Folder` — and the bytes are fetched separately by id via
`OpenReadAsync`. Assets are addressed by id only; storage never sees a client-supplied path.
A typical use is a voice plugin streaming an announcement audio file into a call.

### The `/api/media` endpoints

Operators (and the admin UI) manage media through the authenticated `/api/media` group
(`src/Administration/Api/MediaEndpoints.cs`) — every route requires authorization, a media
permission, and workspace scope:

| Method | Route | Purpose | Permission |
| --- | --- | --- | --- |
| `GET` | `/api/media/?workspaceKey=…&folder=…` | List assets (paged) | `MediaRead` |
| `POST` | `/api/media/?workspaceKey=…&folder=…` | Upload a file | `MediaManage` |
| `GET` | `/api/media/{id}/content?workspaceKey=…` | Stream the bytes | `MediaRead` |
| `DELETE` | `/api/media/{id}?workspaceKey=…` | Delete an asset | `MediaManage` |

Uploads are validated by `MediaUploadPolicy` (allowed content types and size) before
storage.

::: warning Front-end assets ≠ media
Your `main.js`/`main.css` and `.njk` templates are **front-end assets** — published from the
plugin to `/plugin-assets/…`, served anonymously, versioned with the plugin. **Media** are
tenant data — uploaded at runtime, scoped to a workspace, served through the authenticated
`/api/media` API. Don't put runtime uploads in `Resources/public`, and don't ship UI code
through the media library.
:::

## Next steps

- Build the bundle that gets published: **[Building a surface plugin](./building-a-surface-plugin)**
- Author the templates that publish to `views/workspace`: **[SSR Templates](./ssr-templates)**
- How exports (including UI assets) leave a plugin: **[Exporting extensions](/guides/fundamentals/exporting-extensions)**
- The media and UI-chain endpoints: **[REST API](/reference/rest-api)**
