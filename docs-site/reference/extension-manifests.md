# Extension manifests

A plugin package carries several JSON manifests. Some you author
(`registry.json`, `theme.json`, the signed `plugin.signature.json`), one the
platform emits at publish time (`plugin-ui-assets.manifest.json`). This page
documents each format against the real models in the code.

## `registry.json` — the plugin descriptor

Every plugin ships a `registry.json` at its package root describing its identity,
entry point, and contract version. The runtime parses it into
`PluginRegistryJsonDto`
(`src/Core/Infrastructure/Plugins/PluginRegistryJsonDto.cs`); the CLI scaffolder
and signer use a matching model.

### Fields

| Field | Type | Purpose |
| --- | --- | --- |
| `contractVersion` | string | The plugin-contract version the plugin targets (gated at install). |
| `schemaVersion` | string | Schema version of this descriptor. |
| `name` | string | Human-readable plugin name. |
| `pluginId` | string | Stable plugin identifier. |
| `version` | string | Plugin version. |
| `assemblyFileName` | string | Path to the plugin's entry assembly within the package. |
| `entryTypeName` | string | Fully-qualified type name of the runtime entry class. |
| `tier` | string | Deployment tier: `system` (foundation) or `application` (default). |
| `capabilities` | string[] | Capabilities this plugin provides. |
| `requiresCapabilities` | string[] | Capabilities this plugin requires. |
| `dependencies` | object (string→string) | Other plugins this one depends on, by version. |
| `extensions` | array | Extension-point registrations; each has `extensionPointId` and `surface`. |

### Example

```json
{
  "contractVersion": "2.0",
  "schemaVersion": "1.0",
  "name": "My Plugin",
  "pluginId": "my.plugin",
  "version": "1.0.0",
  "assemblyFileName": "bin/Callora.MyPlugin.dll",
  "entryTypeName": "Callora.MyPlugin.PluginEntry",
  "tier": "application",
  "capabilities": ["read:workspaces"],
  "extensions": [
    { "extensionPointId": "callora.item.render", "surface": "workspace" }
  ]
}
```

## `plugin-ui-assets.manifest.json` — the published UI-asset index

When plugins are installed/activated, the host publishes their browser UI assets
and writes a manifest that the shells load to discover entry scripts, styles, and
workspace templates. This manifest is **emitted by the platform**, not authored.

- **Produced by** `PluginUiAssetPublisher.PublishAllAsync`
  (`src/Core/Infrastructure/Plugins/PluginUiAssetPublisher.cs`). It scans active
  installations, copies their assets under the web root's `plugin-assets/`, and
  writes canonical JSON to
  `<webRoot>/plugin-assets/.build/ui-assets.manifest.json` via an atomic
  directory swap (so a crash never leaves a partial manifest).
- **Served at** `/manifests/plugin-ui-assets.manifest.json` — anonymous — by
  `PluginAssetEndpoints` (`src/Administration/Api/PluginAssetEndpoints.cs`). The
  route is configurable via `PluginManifestUrl`; the endpoint returns 404 until a
  manifest has been published.

### Structure

The manifest is an object with a generation timestamp and three collections:

| Field | Type | Purpose |
| --- | --- | --- |
| `generatedAtUtc` | string (ISO 8601) | When the manifest was produced. |
| `entries` | array | Entry-script records. |
| `styleEntries` | array | Stylesheet records. |
| `workspaceTemplates` | array | Workspace template records. |

Each `entries[]` item (`PluginUiAssetManifestEntry`): `pluginId`, `surface`
(`admin` or `workspace`), `entryPath` (relative path to the entry JavaScript).

Each `styleEntries[]` item (`PluginUiStyleManifestEntry`): `pluginId`, `surface`,
`stylePath`.

Each `workspaceTemplates[]` item (`PluginWorkspaceTemplateManifestEntry`):
`pluginId`, `templatePath`.

> **Status:** the exact style/template property names (`stylePath`,
> `templatePath`) were reported from the entry-record models; confirm against
> `PluginUiStyleManifestEntry.cs` / `PluginWorkspaceTemplateManifestEntry.cs` if
> you need byte-exact field casing.

### Example

```json
{
  "generatedAtUtc": "2026-07-19T12:34:56.1234567+00:00",
  "entries": [
    { "pluginId": "my.plugin", "surface": "workspace", "entryPath": "my.plugin/app/workspace/main.js" }
  ],
  "styleEntries": [
    { "pluginId": "my.plugin", "surface": "workspace", "stylePath": "my.plugin/app/workspace/main.css" }
  ],
  "workspaceTemplates": [
    { "pluginId": "my.plugin", "templatePath": "my.plugin/views/workspace/dashboard.html" }
  ]
}
```

## `plugin.signature.json` — the signed content manifest

Package integrity is established by a signed content manifest (ADR-013). Rather
than Authenticode (broken on Linux), Callora signs a canonical manifest of file
hashes with **ECDSA over the NIST P-256 curve, hashing with SHA-256**. Trust is
by the signer's public-key fingerprint, not a certificate chain.

Models live in `src/Core/Application/Plugins/Signing/`:
`PluginSignatureManifest`, `PluginSignatureFileHash`,
`PluginSignatureManifestSerializer`, `PluginSignatureAlgorithms`,
`PluginSignatureCryptography`.

### Fields (`PluginSignatureManifest`)

| Field | Type | Purpose |
| --- | --- | --- |
| `schemaVersion` | string | Manifest schema version. |
| `pluginId` | string | The plugin being signed. |
| `version` | string | Plugin version (from `registry.json`). |
| `algorithm` | string | Always `ECDSA-P256-SHA256`. |
| `signerFingerprint` | string | SHA-256 (hex) of the signer's public key (`SubjectPublicKeyInfo`). |
| `files` | array | One `{ "path", "sha256" }` per covered file. |
| `signature` | string | Base64 ECDSA signature over the canonical manifest (absent before signing). |

Each `files[]` item (`PluginSignatureFileHash`): `path` (plugin-root-relative)
and `sha256` (hex SHA-256 of the file contents).

### Canonical signing

`PluginSignatureManifestSerializer` produces a deterministic canonical form
(files sorted by path, no incidental whitespace) covering the six fields above —
the `signature` field is **excluded** from the signed bytes. This makes signing
and verification agree regardless of on-disk formatting. The algorithm id is
`PluginSignatureAlgorithms.EcdsaP256Sha256`.

### Producing it: `callora plugin sign`

The CLI command signs a plugin directory:

```text
callora plugin sign --plugin <plugin-directory> --key <private-key.pem> [--out <plugin.signature.json>]
```

`PluginSigner` (`src/Host/Cli/Application/PluginSigner.cs`) hashes every package
file except `plugin.signature.json` itself, loads the ECDSA private key from PEM,
builds the manifest, signs the canonical bytes, and writes `plugin.signature.json`
to the plugin root (or `--out`).

### Trust by fingerprint

Verification resolves `signerFingerprint` against the configured trusted signers
via `IPluginSignatureTrustStore`
(`ConfiguredPluginSignatureTrustStore`). A trusted signer is a
`TrustedPluginSigner` with `publisherId`, `displayName`, `thumbprint`
(fingerprint), and `source`. If the fingerprint matches a trusted signer that
carries a public-key PEM, the ECDSA signature can be verified; otherwise
verification fails closed (an untrusted or key-less signer cannot pass).
Configured signers surface at `GET /api/plugins/security/trusted-signers`, and a
per-plugin verification report at `GET /api/plugins/signature-report`.

### Example

```json
{
  "schemaVersion": "1.0",
  "pluginId": "my.plugin",
  "version": "1.0.0",
  "algorithm": "ECDSA-P256-SHA256",
  "signerFingerprint": "A1B2C3D4E5F6...",
  "files": [
    { "path": "registry.json", "sha256": "ABCD1234..." },
    { "path": "bin/Callora.MyPlugin.dll", "sha256": "EF0987AB..." }
  ],
  "signature": "MEQCIBn5Q7K..."
}
```

## `theme.json` — theme templates and setting tokens

A theme plugin ships a `theme.json` describing its template definitions and its
configurable setting fields. On install, the host reads it via
`ThemeJsonWorkspaceTemplateSyncService`
(`src/Core/Infrastructure/Extensions/ThemeJsonWorkspaceTemplateSyncService.cs`)
and syncs definitions into the workspace-template and theme-settings registries.
The operator theme API then exposes and applies them (see the theme endpoints in
[REST API](rest-api.md)).

### Structure

- **Root**
  - `surface` — default surface for the templates (e.g. `workspace`).
  - `definitions` (or `templates`) — an array of template definitions.
  - `config.fields` — an object of setting definitions keyed by setting key.

- **Template definition** — `templateKey` (aliases `key`/`id`), `displayName`,
  `templatePath` (default `views/workspace/{templateKey}.html`),
  `parentTemplateKey`/`extends`, `surface`, `scope` (default `workspace`),
  `isActive` (default true), `priority` (default 100).

- **Setting field** (keyed by setting key) — `label`, `type` (default `text`,
  e.g. `color`/`select`), `helpText`/`description`, `value`/`defaultValue`,
  `required`, `group`/`tab`/`section`, `options`, `order`.

Synced values are surfaced through the theme API as a settings response
(`WorkspaceThemeSettingsApiResponse`): `themePluginId`, `themeVersion`, the field
definitions, and the current `valuesByKey` (key → serialized value). The public
route `GET /workspace/public/theme` exposes a workspace's resolved
`valuesByKey` tokens anonymously.

> **Status:** `theme.json` parsing is intentionally lenient and accepts several
> field aliases (`key`/`id`, `templates`/`definitions`, `help`/`description`,
> etc.). The names above are the canonical ones; the sync service tolerates the
> documented aliases.

### Example

```json
{
  "surface": "workspace",
  "definitions": [
    { "templateKey": "custom-dashboard", "displayName": "Custom Dashboard", "priority": 50 }
  ],
  "config": {
    "fields": {
      "primaryColor": { "label": "Primary Color", "type": "color", "value": "#007bff", "required": true, "order": 10 },
      "layoutMode":  { "label": "Layout Mode",  "type": "select", "value": "default", "options": ["default", "compact", "wide"], "group": "Layout", "order": 20 }
    }
  }
}
```
