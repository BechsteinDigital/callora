# REST API

The Callora host is an ASP.NET application built on minimal APIs
(`MapGet`/`MapPost`/`MapPut`/`MapDelete`, grouped with `MapGroup`). This page
catalogues the real HTTP endpoints, grouped by area.

## Conventions

- **Base paths.** Operator/admin resources live under `/api/*`. Authentication
  lives under `/api/auth` and `/workspace/auth`. Workspace-facing public routes
  live under `/workspace/*`. The server-rendered public surface is `/surface/render`.
  The published UI-asset manifest is served under `/manifests/*`.
- **Auth model.** Login (`/api/auth/login`) issues a JWT carried in an auth
  cookie. Roles: **SuperAdmin** is global; **Admin** is scoped per workspace.
  Fine-grained access is enforced with permission claims (for example
  `plugin.read`, `workspace.update`). A **SuperAdmin** bypasses per-permission
  checks. Workspace-scoped endpoints additionally bind the request to a
  `workspaceKey` and verify the caller's access to that workspace at runtime.
- **Reserved route prefixes.** The public workspace catch-all treats these
  prefixes as reserved (not routed to a workspace): `api`, `swagger`,
  `workspace`, `health`, `plugin-assets`, `manifests`, `_nuxt`. Requests under
  `/admin` are redirected to the admin shell.

> The permission constants shown below are the string keys from
> `BackendPermissionKeys`. "Authenticated" means the group requires a valid
> session but no specific permission. "Anonymous" means no session is required.

## Authentication

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| POST | `/api/auth/login` | Shared admin login; omit `workspaceKey` for a platform-scoped operator session, name it for a workspace-scoped session. Sets the auth cookie and returns a bearer token. | Anonymous (rate-limited) |
| POST | `/api/auth/logout` | Clears the auth cookie. | Anonymous (rate-limited) |
| GET | `/api/auth/me` | Returns the current identity (user id, display name, email, role). | Authenticated |
| POST | `/workspace/auth/login` | Deprecated alias of `/api/auth/login`, retained for the existing workspace shell. | Anonymous (rate-limited) |

## Operator / Admin console

### Admin context

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/admin/context` | Console bootstrap context for the current session. | Authenticated |

### Plugins

Group `/api/plugins`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/plugins/` | List runtime plugins. | `plugin.read` |
| GET | `/api/plugins/installed` | List installed plugin records. | `plugin.read` |
| GET | `/api/plugins/signature-report` | Signature verification report per installed plugin. | `plugin.read` |
| GET | `/api/plugins/audit` | Plugin lifecycle audit log. | `plugin.read` |
| GET | `/api/plugins/contracts/support` | Contract-version support status (v2 supported, v1 deprecated, v0 removed). | `plugin.read` |
| GET | `/api/plugins/contracts/compatibility` | Contract compatibility of installed plugins. | `plugin.read` |
| GET | `/api/plugins/security/trusted-signers` | Configured trusted plugin signers. | `plugin.read` |
| GET | `/api/plugins/workspaces/{workspaceKey}/entitlements/{pluginId}` | Plugin entitlement for a workspace. | `plugin.read` |
| GET | `/api/plugins/tenants/{tenantId}/entitlements/{pluginId}` | Plugin entitlement for a tenant. | `plugin.read` |
| POST | `/api/plugins/install` | Install a plugin. | `plugin.create` |
| POST | `/api/plugins/install/local` | Install a plugin from a local path. | `plugin.create` |
| POST | `/api/plugins/install/nuget` | Install a plugin from a NuGet package. | `plugin.create` |
| POST | `/api/plugins/{pluginId}/update/nuget` | Update a plugin from NuGet. | `plugin.create` |
| POST | `/api/plugins/{pluginId}/update/local` | Update a plugin from a local path. | `plugin.create` |
| POST | `/api/plugins/{pluginId}/activate` | Activate a plugin (hot, no restart). | `plugin.execute` |
| POST | `/api/plugins/{pluginId}/deactivate` | Deactivate a plugin. | `plugin.execute` |
| DELETE | `/api/plugins/{pluginId}` | Uninstall a plugin. | `plugin.delete` |

### Plugin admin extensions

Group `/api/ext/admin`. Plugins contribute admin navigation and routes; the host
serves them dynamically.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/ext/admin/navigation` | Plugin-contributed admin navigation, filtered per-item by the item's required permission. | Authenticated |
| GET/POST/PUT/DELETE | `/api/ext/admin/plugins/{pluginId}/{**routePath}` | Proxy to a plugin's admin route; each route declares its own required permission, checked at runtime. | Per-route permission |

### RBAC

Group `/api/security/rbac`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/security/rbac/roles` | List roles. | `role.read` |
| GET | `/api/security/rbac/permissions` | List available permissions (includes plugin-contributed ones). | `role.read` |
| PUT | `/api/security/rbac/roles/{role}` | Create or update a role. | `role.update` |
| DELETE | `/api/security/rbac/roles/{role}` | Delete a role. | `role.update` |
| GET | `/api/security/rbac/users` | List RBAC user-role assignments. | `role.read` |
| PUT | `/api/security/rbac/users/{userId}` | Assign roles to a user. | `role.update` |
| DELETE | `/api/security/rbac/users/{userId}` | Remove a user's role assignments. | `role.update` |

### Users

Group `/api/users`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/users/` | List users. | `user.read` |
| GET | `/api/users/{userId}` | Get a user. | `user.read` |
| POST | `/api/users/` | Create a user (also requires operator scope at runtime). | `user.create` |
| PUT | `/api/users/{userId}` | Update a user. | `user.update` |
| DELETE | `/api/users/{userId}` | Delete a user. | `user.delete` |
| GET | `/api/users/{userId}/data-export` | Export a user's data (GDPR subject access). | `user.read` |

### Tenants

Group `/api/tenants`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/tenants/` | List tenants. | `tenant.read` |
| GET | `/api/tenants/{tenantKey}` | Get a tenant. | `tenant.read` |
| POST | `/api/tenants/` | Create a tenant. | `tenant.create` |
| POST | `/api/tenants/{tenantKey}/activate` | Activate a tenant. | `tenant.update` |
| POST | `/api/tenants/{tenantKey}/suspend` | Suspend a tenant. | `tenant.update` |
| DELETE | `/api/tenants/{tenantKey}` | Delete a tenant. | `tenant.delete` |

### System configuration

Group `/api/config`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/config/definitions` | Configuration field definitions (host + plugin). | `config.read` |
| GET | `/api/config/effective` | Effective configuration values (workspace-scoped). | `config.read` |
| PUT | `/api/config/values` | Upsert configuration values (workspace-scoped; access verified at runtime). | `config.update` |

### Features

Group `/api/features`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/features/` | List feature flags. | Authenticated |
| GET | `/api/features/{key}` | Get a single feature flag. | Authenticated |

### Business events

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/events/catalog` | The registered business-event catalogue. | `flow.read` |

### Jobs

Group `/api/jobs`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/jobs/` | List background jobs (operators see all; workspace users see their workspace only). | `job.read` |

### Notifications

Group `/api/notifications`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/notifications/` | List notifications (workspace-scoped). | `notification.read` |
| PUT | `/api/notifications/{id}/read` | Mark a notification read (access verified at runtime). | `notification.read` |

### Media

Group `/api/media`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/media/` | List media (workspace-scoped). | `media.read` |
| POST | `/api/media/` | Upload media (workspace-scoped). | `media.manage` |
| GET | `/api/media/{id}/content` | Download media content. | `media.read` |
| DELETE | `/api/media/{id}` | Delete media. | `media.manage` |

### Custom fields

Group `/api/custom-fields`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/custom-fields/definitions` | Custom-field definitions. | `customfield.read` |
| GET | `/api/custom-fields/{entityName}/{entityId}` | Custom-field values for an entity. | `customfield.read` |
| PUT | `/api/custom-fields/{entityName}/{entityId}` | Upsert custom-field values for an entity. | `customfield.update` |

### Flows

Group `/api/flows`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/flows/` | List flows (workspace-scoped). | `flow.read` |
| POST | `/api/flows/` | Create a flow. | `flow.manage` |
| PUT | `/api/flows/{id}` | Update a flow. | `flow.manage` |
| DELETE | `/api/flows/{id}` | Delete a flow. | `flow.manage` |

### Webhooks

Group `/api/webhooks`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/webhooks/` | List webhook subscriptions (workspace-scoped). | `webhook.read` |
| POST | `/api/webhooks/` | Create a webhook subscription. | `webhook.manage` |
| PUT | `/api/webhooks/{id}/activation` | Activate/deactivate a webhook. | `webhook.manage` |
| DELETE | `/api/webhooks/{id}` | Delete a webhook. | `webhook.manage` |

### Entitlements

Group `/api/entitlements`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/entitlements/` | List plugin entitlements. | `plugin.read` |
| PUT | `/api/entitlements/` | Upsert an entitlement. | `plugin.execute` |
| POST | `/api/entitlements/sync` | Sync entitlements from the marketplace/provider. | `plugin.execute` |

## Workspaces and surfaces

### Workspaces

Group `/api/workspaces`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/workspaces/` | List workspaces. | `workspace.read` |
| GET | `/api/workspaces/{workspaceKey}` | Get a workspace. | `workspace.read` |
| PUT | `/api/workspaces/{workspaceKey}` | Create or update a workspace. | `workspace.update` |
| DELETE | `/api/workspaces/{workspaceKey}` | Delete a workspace. | `workspace.delete` |
| PUT | `/api/workspaces/{workspaceKey}/surface-access-policy` | Set the surface access policy (`{ "policy": "Public" \| "Authenticated" }`; unknown value → `400`). | `workspace.update` |
| GET | `/api/workspaces/{workspaceKey}/members` | List workspace members (cursor-paginated). | `workspace.read` |
| PUT | `/api/workspaces/{workspaceKey}/members/{userId}` | Add or update a member. | `workspace.update` |
| DELETE | `/api/workspaces/{workspaceKey}/members/{userId}` | Remove a member. | `workspace.update` |

### Surfaces

Group `/api/workspaces/{workspaceKey}/surfaces`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `.../surfaces/` | List a workspace's surfaces. | `workspace.read` |
| GET | `.../surfaces/{surfaceKey}` | Get a surface. | `workspace.read` |
| PUT | `.../surfaces/{surfaceKey}` | Create or update a surface. | `workspace.update` |
| DELETE | `.../surfaces/{surfaceKey}` | Delete a surface. `409` when it has children. | `workspace.update` |

A surface upsert carries its place in the tree: `parentSurfaceKey` (empty for an application
root), `position` among siblings, and `requiredClaims` for who may see it. A child's
`publicPathPrefix` is **its own segment only** — the full path is composed from the chain, so
moving a subtree does not rewrite its descendants.

Deleting is refused with `409` in two cases, and both mean the same thing — there is a decision
to make first: the node still has children (move or delete them), or it is an application root
(roots carry host, access mode and identity provider and are removed deliberately).

### Themes (operator)

Group `/api/themes`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/themes/definitions` | List theme template definitions. | `extension.read` |
| PUT | `/api/themes/definitions/{templateKey}/plugins/{pluginId}/versions/{version}` | Upsert a theme definition. | `extension.update` |
| PUT | `/api/themes/definitions/{templateKey}/plugins/{pluginId}/versions/{version}/activation` | Toggle a theme definition's activation. | `extension.update` |
| GET | `/api/themes/workspaces/{workspaceKey}` | Get a workspace's assigned theme. | `extension.read` |
| PUT | `/api/themes/workspaces/{workspaceKey}` | Assign a theme to a workspace. | `extension.update` |
| DELETE | `/api/themes/workspaces/{workspaceKey}` | Clear a workspace's theme. | `extension.update` |
| GET | `/api/themes/workspaces/{workspaceKey}/effective` | Effective theme (resolved chain). | `extension.read` |
| GET | `/api/themes/workspaces/{workspaceKey}/settings` | Theme setting fields + current values. | `extension.read` |
| PUT | `/api/themes/workspaces/{workspaceKey}/settings` | Upsert theme setting values. | `extension.update` |

### Themes (workspace)

Group `/workspace/themes`.

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/workspace/themes/effective` | Effective theme for the caller's workspace (access verified at runtime). | `extension.read` |

## Public workspace surface

These routes serve the public storefront-style surface and are excluded from the
OpenAPI description. Most are unconditionally anonymous; `/surface/render` and
`/workspace/public/ui-chain` additionally enforce the workspace's **surface access
policy** — `Public` (default) is anonymous, `Authenticated` turns anonymous callers
away (see [Access policy](/guides/surface/ssr-templates#access-policy)).

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/workspace/public/resolve` | Resolve the request host/path to a workspace (returns `resolved`, `workspaceKey`). | Anonymous |
| GET | `/workspace/public/bootstrap.js` | JavaScript that sets `window.__CALLORA_WORKSPACE_CONTEXT__`. | Anonymous |
| GET | `/workspace/public/context` | Workspace + route context for a given `path`. | Anonymous |
| GET | `/workspace/public/ui-chain` | The workspace's resolved UI-chain (plugin composition). | Anonymous (Public); `Authenticated` → `404` for anonymous |
| GET | `/workspace/public/theme` | The workspace's public theme tokens (`valuesByKey`). | Anonymous |
| GET | `/login` | Redirect to the workspace shell login for the resolved workspace. | Anonymous |
| GET | `/surface/render` | Server-render the workspace's template chain (falls back to the SPA shell). | Anonymous (Public); `Authenticated` → `302 /login` for anonymous |
| GET | `/` and `/{**path:nonfile}` | Catch-all: redirect to the workspace shell, the admin shell (`/admin`), or 404 for reserved prefixes. | Anonymous |

## Plugin assets and manifests

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/manifests/plugin-ui-assets.manifest.json` | The published UI-asset manifest (route configurable via `PluginManifestUrl`). Returns 404 until published. | Anonymous |
| GET | `/admin/{**path:nonfile}` | Admin-shell SPA fallback (serves the shell's `index.html`). | Anonymous |

Published plugin UI assets themselves are served as static files under
`/plugin-assets/*`. See [Extension manifests](extension-manifests.md) for the
manifest format.

## Health

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/health` | Liveness probe. Returns `{"status":"ok"}` (this JSON body is a contract). | Anonymous |
| GET | `/ready` | Readiness probe (checks the database). | Anonymous |

## Dynamic plugin routes

Active plugins contribute their own API routes through the platform's route
data source (Shopware-style discovery): activating a plugin adds its routes and
deactivating removes them, without a host restart. These routes are not part of
this static catalogue because they are defined by the installed plugins.
