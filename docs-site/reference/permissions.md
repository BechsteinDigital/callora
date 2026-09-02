# Permissions

The Callora host authorizes every operator/admin API call against a fixed set of
RBAC permission keys. This page catalogues **all 38 permission keys** — the key
string, its C# constant, and the endpoints that require it — and explains how the
RBAC model, the `callora_scope` claim, and the SuperAdmin bypass fit together.

## The RBAC model

Authorization has two orthogonal parts: **scope** (how far a session reaches) and
**permissions** (which actions it may perform).

- **Scope** is the `callora_scope` claim (`BackendClaimTypes.CalloraScope`), stamped
  at token issuance. Its two tiers are *platform* and *workspace*. A platform-scoped
  session (a platform operator) reaches across every workspace; a workspace-scoped
  session is bound to its `workspace_key` claim and can only touch that workspace.
  A principal without the scope claim never gains platform-wide access (fail-closed).
- **Permissions** are `permission` claims (`BackendClaimTypes.Permission`) carrying the
  keys below. An endpoint guarded by `RequirePermission(...)` allows the call only if
  the caller holds the matching key (or the `*` wildcard).

::: info Scope is reach, not authority
Being a platform operator grants **scope**, not blanket authority. Only the
**SuperAdmin** role satisfies permission checks unconditionally. Any other operator
role (e.g. an API-key `host.api` role, or a custom operator role) still draws its
concrete rights from its permission grants — an operator role with platform scope but
no permission grants reaches every workspace yet is denied every permission-gated
action (`403`).
:::

### Roles

Role names live in `Callora.Core.Application.Security.BackendRoles`:

| Role constant | Value | Scope | Notes |
| --- | --- | --- | --- |
| `BackendRoles.SuperAdmin` | `superadmin` | Platform | Unrestricted global backend access. **The only role that satisfies permission checks unconditionally.** Seeded with the `*` wildcard grant. |
| `BackendRoles.Admin` | `admin` | Workspace | Workspace administrator — **not** a global operator; grants are carried per workspace via `WorkspaceMembership`. |
| `BackendRoles.HostApi` | `host.api` | Platform | Role for API-key host access; treated as a platform operator (scope, not authority). |

::: warning Global identity vs. workspace membership
`user.*` **write** keys govern the *global* `BackendUser` — credentials, account
erasure and the data-subject export. Those operations reach every workspace the
subject belongs to, so the endpoints additionally require **platform scope**: a
workspace-scoped session is rejected with `403` even while holding the key.

Workspace administration uses `membership.*` instead, which manages only who
belongs to a workspace and in which workspace role. A workspace-bound caller
reaches only its own workspace; any other `{workspaceKey}` answers `404`.
`WorkspaceRolePermissions` therefore grants the workspace `admin` role
`membership.read/update/delete` and `user.read`, never a `user.*` write key.
:::

### SuperAdmin bypass and seeding

`BackendRbacDatabaseSeeder` (in `Callora.Core.Infrastructure.Persistence`) ensures the
`superadmin` role exists as a system role (`IsSystem = true`) holding a single grant with
`PermissionKey = "*"`. The permission check
(`Callora.Core.Infrastructure.Security.EndpointAuthorizationExtensions`) short-circuits to
allow when `user.IsInRole(BackendRoles.SuperAdmin)`; otherwise it requires a matching
`permission` (or `scope`) claim, accepting either an exact key match or `*`.

The seeder also assigns the demo admin user and the one-time bootstrap operator
(`InitialOperator`) to the `superadmin` role. Non-SuperAdmin roles derive their permissions
from `BackendHostOptions.RbacRoles` via `BackendRbacPermissionCatalog`.

### Keys a plugin brings with it

The keys above are the host's. A plugin declares its own in its
[`registry.json`](/guides/fundamentals/registry-manifest#declaring-the-permissions-your-routes-require),
because `CalloraRouteAttribute.Permission` lets a route demand a key and nothing else could
supply one.

Declared keys appear in `GET /api/security/rbac/permissions` alongside the host's, so an
operator grants them by picking them — the same list, whichever way the plugin supplied them.
A plugin contributing Admin-API routes could always supply keys through
`IHostAdminApiExtensionContributor.PermissionKeys`; a plugin whose surface is
`[CalloraRoute]` controllers had no such path, and its routes demanded keys nobody could
grant.

A plugin may only declare keys **inside its own namespace** (`<pluginId>.…`) that **end in a
known action**. Both are enforced when the manifest is read, and either violation makes the
manifest invalid. The namespace rule is the important one: declaration is self-service, and
without it a plugin could declare `user.delete` and be granted the host's permission by an
operator who believed they were granting the plugin's own.

The two rules are separable, and the split matters. `PluginPermissionKeyPolicy.IsInsideNamespace`
holds the boundary — reserved host namespaces, lower case, own prefix — and is applied to **both**
supply paths wherever keys are attributed to a plugin. The action vocabulary is applied to the
manifest only: keys contributed through `IHostAdminApiExtensionContributor` never passed it and never
had to, and `composer.layout.publish` and `communication.accounts.manage` are in service today.

### A workspace administrator and the plugins of their workspace

A workspace-scoped session used to carry `WorkspaceRolePermissions.ForRole` and nothing else — a
fixed list of core keys — and `BackendClaimsTransformation` deliberately returns early for workspace
scope, so an RBAC role could not supply one either. A plugin key could therefore reach a workspace
session on **no** path: every plugin screen was empty for everybody but the super admin, whatever role
they held. Enforcement worked; granting was impossible.

The `admin` workspace role now additionally carries the keys of the plugins **activated in that
workspace**. Two boundaries make that defensible, and both are load-bearing:

- **Activation, not installation.** A plugin sitting on the machine but inactive in this workspace
  supplies nothing. A workspace administrator gets their workspace's plugins, never the
  installation's.
- **The plugin's own namespace.** Keys are attributed per plugin and filtered to `<pluginId>.…`
  before they reach a session. The manifest path has always refused a foreign key; the contributor
  path never had that boundary, and without it a plugin could have written `user.delete` — a key that
  reaches past the workspace — into a workspace administrator's session.

A **member** keeps the read-only floor. Finer cuts than "administrator" need roles a plugin names for
itself; guessing a split here would look considered and therefore go unchecked.

Permissions live in the token, so a plugin activated after sign-in takes effect at the next one — the
same as any other permission change.

### One evaluator, three call sites

All three enforcement paths — the minimal-API `RequirePermission(...)`, the MVC
`[CalloraPermission(...)]` attribute, and plugin routes declaring
`CalloraRouteAttribute.Permission` — ask `UserHasPermission` and nothing else. A test fails
the build if a fourth site compares permission claims on its own.

### What a refusal says

A `403` from any of the three is a problem document naming the key that was missing:

```json
{
  "type": "https://callora.dev/problems/forbidden",
  "title": "Forbidden",
  "status": 403,
  "detail": "The permission 'plugin.execute' is required.",
  "missingPermission": "plugin.execute"
}
```

The key is deliberately disclosed. The caller already knows which endpoint it called, this
page publishes every key, and without it an operator debugging a role grant bisects the
catalogue by hand.

## The permission-key catalogue

All keys are defined as `public const string` in
`Callora.Core.Application.Security.BackendPermissionKeys`. The table below lists every key,
its constant, and the endpoint(s) that enforce it — via the minimal-API
`RequirePermission(...)` filter or the equivalent `[CalloraPermission(...)]` attribute on
MVC controllers.

::: warning `diagnostics.record` discloses more than the other monitoring keys
A recording contains **SQL command text**, including literals EF Core inlines. That is a
wider disclosure than any other monitoring endpoint makes, which is why it is a key of its
own rather than folded into `job.read`. Grant it to the people who debug the platform, not
to everyone who may watch it.
:::

| Permission key | C# constant | Authorizes (endpoints) |
| --- | --- | --- |
| `tenant.create` | `TenantCreate` | `POST /api/tenants` |
| `tenant.read` | `TenantRead` | `GET /api/tenants`, `GET /api/tenants/{tenantKey}` |
| `tenant.update` | `TenantUpdate` | `POST /api/tenants/{tenantKey}/activate`, `POST /api/tenants/{tenantKey}/suspend` |
| `tenant.delete` | `TenantDelete` | `DELETE /api/tenants/{tenantKey}` |
| `plugin.create` | `PluginCreate` | `POST /api/plugins/install`, `/install/local`, `/install/nuget`, `POST /api/plugins/{id}/update/nuget`, `/update/local` |
| `plugin.read` | `PluginRead` | `GET /api/plugins`, `/installed`, `/signature-report`, `/audit`, `/contracts/support`, `/contracts/compatibility`, `/security/trusted-signers`, `/workspaces/{wk}/entitlements/{pid}`, `/tenants/{tid}/entitlements/{pid}`; `GET /api/entitlements` |
| `plugin.delete` | `PluginDelete` | `DELETE /api/plugins/{id}` |
| `plugin.execute` | `PluginExecute` | `POST /api/plugins/{id}/activate`, `/deactivate`; `PUT /api/entitlements`; `POST /api/entitlements/sync` |
| `config.read` | `ConfigRead` | `GET /api/config/definitions`, `GET /api/config/effective` |
| `config.update` | `ConfigUpdate` | `PUT /api/config/values` |
| `webhook.read` | `WebhookRead` | `GET /api/webhooks` |
| `webhook.manage` | `WebhookManage` | `POST /api/webhooks`, `PUT /api/webhooks/{id}/activation`, `DELETE /api/webhooks/{id}` |
| `notification.read` | `NotificationRead` | `GET /api/notifications`, `PUT /api/notifications/{id}/read` |
| `media.read` | `MediaRead` | `GET /api/media`, `GET /api/media/{id}/content` |
| `media.manage` | `MediaManage` | `POST /api/media`, `DELETE /api/media/{id}` |
| `customfield.read` | `CustomFieldRead` | `GET /api/custom-fields/definitions`, `GET /api/custom-fields/{entityName}/{entityId}` |
| `customfield.update` | `CustomFieldUpdate` | `PUT /api/custom-fields/{entityName}/{entityId}` |
| `flow.read` | `FlowRead` | `GET /api/flows`; `GET /api/events/catalog` |
| `flow.manage` | `FlowManage` | `POST /api/flows`, `PUT /api/flows/{id}`, `DELETE /api/flows/{id}` |
| `job.read` | `JobRead` | `GET /api/jobs` |
| `diagnostics.record` | `DiagnosticsRecord` | `POST /api/diagnostics/recorder/start`, `POST /api/diagnostics/recorder/stop`, `GET /api/diagnostics/recorder` |
| `extension.read` | `ExtensionRead` | `GET /api/themes/definitions`, `/workspaces/{wk}`, `/workspaces/{wk}/effective`, `/workspaces/{wk}/settings`; `GET /workspace/themes/effective` |
| `extension.update` | `ExtensionUpdate` | `PUT /api/themes/definitions/{templateKey}/plugins/{pluginId}/versions/{version}` (and its `/activation`), `PUT /api/themes/workspaces/{wk}`, `DELETE /api/themes/workspaces/{wk}`, `PUT /api/themes/workspaces/{wk}/settings` |
| `role.read` | `RoleRead` | `GET /api/rbac/roles`, `/permissions`, `/users` |
| `role.update` | `RoleUpdate` | `PUT /api/rbac/roles/{role}`, `DELETE /api/rbac/roles/{role}`, `PUT /api/rbac/users/{userId}`, `DELETE /api/rbac/users/{userId}` |
| `user.create` | `UserCreate` | `POST /api/users` — **platform operators only** |
| `user.read` | `UserRead` | `GET /api/users`, `GET /api/users/{userId}` (workspace-filtered), `GET /api/users/{userId}/data-export` (**platform operators only**) |
| `user.update` | `UserUpdate` | `PUT /api/users/{userId}`, `PUT /api/users/{userId}/activation` — **platform operators only** |
| `user.delete` | `UserDelete` | `DELETE /api/users/{userId}` — **platform operators only** |
| `membership.read` | `MembershipRead` | `GET /api/workspaces/{wk}/members` |
| `membership.update` | `MembershipUpdate` | `PUT /api/workspaces/{wk}/members/{userId}` |
| `membership.delete` | `MembershipDelete` | `DELETE /api/workspaces/{wk}/members/{userId}` |
| `workspace.create` | `WorkspaceCreate` | — *(defined; no endpoint currently enforces it)* |
| `workspace.read` | `WorkspaceRead` | `GET /api/workspaces`, `/{wk}`, `/{wk}/members`; `GET /api/surfaces`, `/{surfaceKey}` |
| `workspace.update` | `WorkspaceUpdate` | `PUT /api/workspaces/{wk}`, `PUT /api/workspaces/{wk}/members/{userId}`, `DELETE /api/workspaces/{wk}/members/{userId}`; `PUT /api/surfaces/{surfaceKey}`, `DELETE /api/surfaces/{surfaceKey}` |
| `workspace.delete` | `WorkspaceDelete` | `DELETE /api/workspaces/{wk}` |
| `integration.read` | `IntegrationRead` | `GET /api/security/integrations` |
| `integration.manage` | `IntegrationManage` | `POST /api/security/integrations`, `DELETE /api/security/integrations/{id}` |

::: tip Key naming
Keys follow a `{function}.{action}` shape where the action is one of
`create` / `read` / `update` / `delete` / `execute`
(`BackendPermissionActions`). Custom operator roles built from
`BackendHostOptions.RbacRoles` compose their grants from the same catalogue.
:::

> **Status:** `workspace.create` is a defined constant but is not yet enforced by any
> endpoint — workspaces are created through other flows today. It is reserved for a future
> dedicated endpoint.

## Plugin controllers use the same keys

A plugin that contributes backend routes annotates them with
`[CalloraRoute(..., Permission = "…")]`. The `Permission` string is one of the same
keys above (or a plugin-defined key). At dispatch, the host
(`PluginApiEndpointDataSource`) enforces it: the caller must hold a matching
`permission` claim (or `*`), otherwise the request is rejected with `403` and an
RFC 9457 problem response. An empty `Permission` means authenticated-only.

```csharp
[CalloraRoute("POST", "/dialer/campaigns", Permission = "plugin.execute")]
public Task<IResult> StartCampaign(...) { ... }
```

::: info Plugin routes use the same evaluator
Plugin-route enforcement calls `EndpointAuthorizationExtensions.UserHasPermission` — the same
decision as `RequirePermission(...)` and `[CalloraPermission(...)]`, including the SuperAdmin
role short-circuit and the `scope` claim.

It once compared permission claims inline and did neither. That never surfaced as a bug,
because the seeded `*` grant is stamped as a permission claim, so a SuperAdmin passed the
inline rule too — the two rules agreed by coincidence of the current seeding rather than by
construction.
:::

## See also

- [REST API](rest-api.md) — the full endpoint catalogue these permissions guard.
- [Backend extensions](/guides/backend-extensions) — contributing controllers with
  `[CalloraRoute]` and declaring their required permission.
- [Architecture](/concepts/architecture) — where RBAC sits in the host.
