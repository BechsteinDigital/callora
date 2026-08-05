# Permissions

The Callora host authorizes every operator/admin API call against a fixed set of
RBAC permission keys. This page catalogues **all 37 permission keys** — the key
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

## The permission-key catalogue

All keys are defined as `public const string` in
`Callora.Core.Application.Security.BackendPermissionKeys`. The table below lists every key,
its constant, and the endpoint(s) that enforce it — via the minimal-API
`RequirePermission(...)` filter or the equivalent `[CalloraPermission(...)]` attribute on
MVC controllers.

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
| `extension.read` | `ExtensionRead` | `GET /api/themes/definitions`, `/workspaces/{wk}`, `/workspaces/{wk}/effective`, `/workspaces/{wk}/settings`; `GET /workspace/themes/effective` |
| `extension.update` | `ExtensionUpdate` | `PUT /api/themes/definitions/{templateKey}/plugins/{pluginId}/versions/{version}` (and its `/activation`), `PUT /api/themes/workspaces/{wk}`, `DELETE /api/themes/workspaces/{wk}`, `PUT /api/themes/workspaces/{wk}/settings` |
| `role.read` | `RoleRead` | `GET /api/rbac/roles`, `/permissions`, `/users` |
| `role.update` | `RoleUpdate` | `PUT /api/rbac/roles/{role}`, `DELETE /api/rbac/roles/{role}`, `PUT /api/rbac/users/{userId}`, `DELETE /api/rbac/users/{userId}` |
| `user.create` | `UserCreate` | `POST /api/users` — **platform operators only** |
| `user.read` | `UserRead` | `GET /api/users`, `GET /api/users/{userId}` (workspace-filtered), `GET /api/users/{userId}/data-export` (**platform operators only**) |
| `user.update` | `UserUpdate` | `PUT /api/users/{userId}` — **platform operators only** |
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

::: warning Plugin-route bypass differs
Plugin-route permission enforcement checks the `permission` claim (or `*`) directly.
Unlike the host's `RequirePermission(...)` extension, it does **not** special-case the
SuperAdmin role — a SuperAdmin passes because the seeded `*` grant is stamped as a
permission claim, not because of a role short-circuit.
:::

## See also

- [REST API](rest-api.md) — the full endpoint catalogue these permissions guard.
- [Backend extensions](/guides/backend-extensions) — contributing controllers with
  `[CalloraRoute]` and declaring their required permission.
- [Architecture](/concepts/architecture) — where RBAC sits in the host.
