# Configuration

Callora has two configuration layers: **host configuration** — the operator-set
options that govern the platform (bound into `BackendHostOptions` and
`CalloraHostingOptions`) — and **scoped plugin configuration** — the per-plugin
settings a plugin declares and reads through `IPluginConfigReader`. This page
catalogues both.

## Configuration sources and precedence

Host configuration follows the standard ASP.NET Core provider order, so later
sources override earlier ones:

1. `appsettings.json` / `appsettings.{Environment}.json`
2. Environment variables, using the `Section__Key` double-underscore convention
   (e.g. `BackendHost__ApiKeys__0`)
3. Command-line arguments

`docker-compose.yml` sets the environment variables with `${VAR:-default}`
fallbacks, so an operator can override any value through the shell environment or a
`.env` file without editing the compose file. See `.env.example` for the full set of
overridable variables.

Two sections carry the host's operator options:

- **`BackendHost`** → `BackendHostOptions` (`Callora.Core.Application.Policies`),
  bound via `Configuration.GetSection("BackendHost").Bind(...)`.
- **`CalloraHosting`** → `CalloraHostingOptions` (`Callora.Core.Application.Options`),
  bound via `Configuration.GetSection("CalloraHosting").Bind(...)`. Plugin
  directories live here, **not** in `BackendHost`.

## `BackendHost` options

Bound from the `BackendHost` section into `BackendHostOptions`. Defaults are the
C# property initializers; where a path default is `AppContext.BaseDirectory`-relative
it is shown as a relative fragment.

| Option | Type | Default | Purpose |
| --- | --- | --- | --- |
| `EnableTenantManagementApi` | `bool` | `false` | Enables the tenant management API surface. |
| `DefaultTenantKey` | `string` | `default` | Key of the implicit default tenant. |
| `DefaultTenantDisplayName` | `string` | `Default Tenant` | Display name of the default tenant. |
| `AuditFilePath` | `string` | `plugins/audit-log.jsonl` | JSONL audit-log file for lifecycle/security events. |
| `AdminShellBaseUrl` | `string` | `/admin/` | Base URL under which the admin shell is served. |
| `WorkspaceShellBaseUrl` | `string` | `/` | Base URL under which the workspace shell is served. |
| `PluginAssetBaseUrl` | `string` | `/plugin-assets` | Public base URL for plugin UI assets. |
| `PluginManifestUrl` | `string` | `/manifests/plugin-ui-assets.manifest.json` | URL of the published plugin UI-asset manifest. |
| `RateLimitAuthPerMinute` | `int` | `5` | Login attempts allowed per client per minute; `0` disables. |
| `RateLimitApiPerMinute` | `int` | `600` | General API requests allowed per client per minute; `0` disables. |
| `MediaStoragePath` | `string` | `media` | Root directory for stored media assets. |
| `AllowPrivateWebhookTargets` | `bool` | `false` | Permits webhook targets on private/loopback addresses — **development only** (production keeps the SSRF egress guard on). |
| `DataProtectionKeysPath` | `string` | `dataprotection-keys` | Key-ring directory for ASP.NET DataProtection. Must live on durable storage or every restart loses encrypted secrets. |
| `DatabaseConnectionString` | `string` | `Host=localhost;Port=5432;Database=callora_host;Username=callora;Password=callora` | PostgreSQL connection string for the host database. |
| `RequireAllowlistForActivation` | `bool` | `false` | When true, only allowlisted plugin IDs may be activated. |
| `ActivationAllowlistPluginIds` | `string[]` | `[]` | Plugin IDs permitted to activate when the allowlist is enforced. |
| `ActivationEntitledPluginIds` | `string[]` | `[]` | Plugin IDs with a platform-wide activation entitlement. |
| `ActivationTenantEntitlements` | `BackendTenantPluginEntitlementOptions[]` | `[]` | Per-tenant plugin activation entitlements. |
| `DefaultActivationRolloutRing` | `PluginRolloutRing` | `Stable` | Default rollout ring for activation gating. |
| `ActivationTenantRolloutRings` | `BackendTenantPluginRolloutRingOptions[]` | `[]` | Per-tenant rollout-ring overrides. |
| `EntitlementFailureFallbackMode` | `EntitlementFailureFallbackMode` | `DenyActivation` | Behaviour when an entitlement check fails/errors. |
| `JwtIssuer` | `string` | `callora-local` | Issuer claim for host-issued JWTs. |
| `JwtAudience` | `string` | `callora-host-api` | Audience claim for host-issued JWTs. |
| `JwtSigningKey` | `string` | `BackendSecretHygiene.DefaultJwtSigningKey` | HMAC signing key for JWTs. **Override in production.** |
| `OidcAuthority` | `string?` | `null` | Optional external OIDC authority. |
| `AuthCookieName` | `string` | `callora_admin_auth` | Name of the auth cookie carrying the JWT. |
| `AuthCookieRequireHttps` | `bool` | `false` | Marks the auth cookie `Secure` (HTTPS-only). |
| `EnableBootstrapApiKeys` | `bool` | `true` | Enables the seeded bootstrap API keys. |
| `RequireApiKeyAuthentication` | `bool` | `true` | Requires an API key header on API requests. |
| `ApiKeyHeaderName` | `string` | `X-Callora-Api-Key` | Header carrying the API key. |
| `ApiKeys` | `string[]` | `[]` | Accepted API keys (host-scoped access). |
| `RbacRoles` | `BackendRbacRoleOptions[]` | `[]` | Config-defined RBAC roles and their permission grants. |
| `RbacUserAssignments` | `BackendRbacUserAssignmentOptions[]` | `[]` | Config-defined user→role assignments. |
| `PlatformOperatorRoles` | `string[]` | `["superadmin"]` | Roles permitted to sign in via the platform-operator login and granted platform **scope** (not blanket authority). |
| `AllowedCsrfOrigins` | `string[]` | `[]` | Extra origins accepted for cookie-authenticated state-changing requests (beyond same-origin). Header-authenticated requests are exempt. |
| `ProblemTypeBaseUri` | `string` | `urn:callora:problem:` | Base URI for RFC 9457 problem types. |
| `DefaultPluginEntitlement` | `bool` | `true` | Entitlement verdict when no explicit entitlement row exists. `true` suits self-hosted (every installed plugin usable); cloud sets `false` for explicit grants. |
| `TrustedSignerThumbprints` | `string[]` | `[]` | Legacy trusted-signer certificate thumbprints. |
| `TrustedSigners` | `BackendTrustedSignerOptions[]` | `[]` | Structured trusted signers (publisher id, display name, fingerprint, source). |
| `AllowUnsignedPlugins` | `bool` | `false` | Permits installing/loading unsigned plugins. **Development only.** |
| `RevokedSignerFingerprints` | `string[]` | `[]` | Signer key fingerprints (SHA-256 of the SPKI) that are revoked; enforced at install and at load. |
| `RevokedContentHashes` | `string[]` | `[]` | Revoked plugin assembly content hashes (SHA-256, hex); rejected regardless of signature. |
| `DemoAdminUser` | `BackendDemoAdminUserOptions` | `new()` | Development convenience admin, re-seeded on start when enabled. |
| `InitialOperator` | `BackendInitialOperatorOptions` | `new()` | One-time bootstrap operator, seeded only on a fresh install (no users yet). |
| `FeatureFlags` | `Dictionary<string, bool>` | empty (case-insensitive) | Central name→enabled feature-flag map, queried via `/api/features`. |

::: warning Production hardening
`JwtSigningKey`, `ApiKeys`, and `DatabaseConnectionString` ship with development
defaults. Override them in production, and set `AuthCookieRequireHttps=true`. Note
`docker-compose.yml` sets `AllowUnsignedPlugins=true` for local development — keep it
`false` (the property default) in production.
:::

## `CalloraHosting` options — plugin directories & loading

Bound from the `CalloraHosting` section into `CalloraHostingOptions`. This is where
plugin discovery is configured.

| Option | Type | Default | Purpose |
| --- | --- | --- | --- |
| `PluginDirectory` | `string` | `custom/plugins` (`BaseDirectory`-relative) | Directory scanned for Application-tier runtime plugins when auto-load is on. |
| `StaticPluginDirectory` | `string` | `custom/static-plugins` (`BaseDirectory`-relative) | Directory scanned for System/Foundation-tier bundled plugins; scanned **before** `PluginDirectory`. |
| `AutoLoadPlugins` | `bool` | `false` | Enables automatic plugin discovery/load from the plugin directories. |
| `AutoActivateInstalledPlugins` | `bool` | `true` | Auto-activates installed plugins marked active in runtime state. |
| `AutoBootstrapModules` | `bool` | *see options class* | Auto-bootstraps host modules at startup. |
| `PluginRegistryFilePath` | `string` | `custom/plugins/registry.json` (env default) | Path to the plugin registry file. |

> **Status:** Property-level defaults for `CalloraHostingOptions` are read from the
> class initializers and the `.env.example` / `docker-compose.yml` defaults; the exact
> initializer default for `AutoBootstrapModules` was not pinned here — the compose/env
> default is `true`.

`PluginDirectory` is post-processed through `CalloraHostingPathResolver.ResolvePluginDirectory(...)`
after binding, so a relative path is resolved against the application base directory.

## Other operator options sections

| Section | Options class | Purpose (selected properties) |
| --- | --- | --- |
| `BackgroundJobs` | `BackgroundJobOptions` | Worker/scheduler tuning: `PollInterval` (1s), `SchedulerInterval` (5s), `RetryBaseDelay` (30s), `LeaseDuration` (5m), `RecentListLimit` (100). |
| `Retention` | `RetentionOptions` | Data cleanup: `Enabled` (`true`), `SweepInterval` (6h), `CompletedJobRetention` (14d), `NotificationRetention` (90d). |
| `Observability` | `ObservabilityOptions` | Telemetry: `ServiceName` (`callora-host`), `OtlpEndpoint` (`null` — export disabled when empty). |

## Scoped plugin configuration (SystemConfig)

Beyond host options, a plugin declares and reads **its own** configuration through the
SystemConfig subsystem. Values resolve across a scope chain, most-specific wins:

```text
workspace  >  tenant  >  global  >  definition default
```

Scopes are the constants in `SystemConfigScopes`: `global`, `tenant`, `workspace`.

### Declaring config (registry.json)

A plugin declares its config schema in `registry.json` under a `config.fields` object.
On install/load, `RegistryConfigSchemaSyncService` locates `registry.json` (bounded walk
from the assembly), parses `config.fields`, and persists the definitions via
`ISystemConfigStore.ReplaceDefinitionsForPluginAsync(...)`.

```json
{
  "config": {
    "fields": {
      "preferredCodec": {
        "type": "text",
        "label": "Preferred codec",
        "description": "Codec offered first during negotiation.",
        "default": "opus",
        "group": "Audio",
        "order": 10
      },
      "apiSecret": {
        "type": "secret",
        "label": "API secret",
        "group": "Credentials"
      }
    }
  }
}
```

Field properties map to `SystemConfigDefinitionInput`: `label`, `type`,
`description` / `helpText`, `default` / `value`, `group` / `tab`, `order`
(auto-increments by 10 when omitted), `disabled`, `options`. A field of type
`secret` (`SystemConfigFieldTypes.Secret`) is **encrypted at rest** and never
returned in plaintext — the effective-values API masks it as `"***"`.

### Reading config in a plugin

Plugins read the effective value through `IPluginConfigReader`
(`Callora.Core.Application.Configuration.Contracts`):

| Method | Purpose |
| --- | --- |
| `GetAllAsync(pluginId, workspaceKey?, ct)` | All effective values (JSON-encoded strings). |
| `GetStringAsync(pluginId, configKey, workspaceKey?, ct)` | Raw string value. |
| `GetBoolAsync(pluginId, configKey, fallback, workspaceKey?, ct)` | Boolean with fallback. |
| `GetIntAsync(pluginId, configKey, fallback, workspaceKey?, ct)` | Integer with fallback. |

Under the hood, `SystemConfigResolver` builds the scope chain and reads from
`ISystemConfigStore`, applying the precedence above.

### Operator endpoints

Operators manage values through the `/api/config` surface (see also
[REST API](rest-api.md)):

| Method | Path | Permission | Purpose |
| --- | --- | --- | --- |
| `GET` | `/api/config/definitions` | `config.read` | List config field definitions; optional `pluginId` query. |
| `GET` | `/api/config/effective` | `config.read` (+ workspace scope) | Resolved effective values for a plugin/workspace; secret fields masked as `"***"`. |
| `PUT` | `/api/config/values` | `config.update` | Upsert values — body `{ pluginId, scope, scopeKey?, valuesByKey }`. |

For `PUT /api/config/values`, `scope` is one of `global` / `tenant` / `workspace`;
`scopeKey` is required for `tenant` and `workspace`. Global/tenant writes are
operator-only; workspace writes require access to the target workspace.

## See also

- [Plugin configuration](/guides/fundamentals/plugin-configuration) — the how-to for
  declaring and reading scoped config.
- [REST API](rest-api.md) — the `/api/config` endpoints in the full catalogue.
- [Permissions](permissions.md) — the `config.read` / `config.update` keys.
- [Architecture](/concepts/architecture) — where host options and SystemConfig sit.
