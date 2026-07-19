# Administration

The admin shell at `/admin` is where operators and workspace admins manage the
platform. This page covers the roles that gate everything, then the core
management screens: tenants, users and members, plugins, and system
configuration.

## RBAC: SuperAdmin vs. Admin

Callora separates two kinds of privilege, and the distinction runs through the
whole product.

- **SuperAdmin** (`superadmin`) is the **global operator**. It bypasses
  permission checks (holds the `*` wildcard), can sign in without a workspace,
  and sees and acts across every workspace. Bootstrap accounts and API-key
  callers act as super admins.
- **Admin** is **per-workspace only**. It is *not* a full operator. A workspace
  admin's rights are carried by their membership in one workspace, and their
  session is locked to it.

A third role, `host.api`, is what API-key callers authenticate as; it is treated
as a platform operator for machine-to-machine access.

### How scope is expressed

Every session carries a scope claim, `callora_scope`, with one of two values:

- `platform` — a platform-operator session. Issued to super admins (and API
  keys). The workspace key is ignored; the session reaches all workspaces.
- `workspace` — a workspace-scoped session. Issued by workspace login and locked
  to the workspace named in the `workspace_key` claim.

Login resolution decides the scope: if you hold a platform-operator role you get
`platform` scope; otherwise, if you are a member of the named workspace, you get
`workspace` scope; otherwise the login is refused. The nav hides links you
cannot use, but the server is authoritative — cross-workspace access from a
workspace session returns 404, not a leak.

### Managing roles

Roles and their permission sets live under `/roles` in the shell (API base
`/api/security/rbac`): list roles and permissions, upsert a role with its
permission keys, and assign global roles to users. These endpoints manage
**global** RBAC and require the `role.*` permissions — deliberately distinct
from the workspace-admin `user.*` permissions, so a workspace admin cannot
escalate to operator.

Permission keys follow a `<function>.<action>` shape (for example `role.read`,
`workspace.read`, `plugin.read`). Plugins can contribute their own permission
keys.

## Tenants

A **tenant** is the billing/ownership boundary — who pays. Manage tenants under
`/tenants` (Mandanten):

| Action | Endpoint |
|---|---|
| List | `GET /api/tenants` |
| Get | `GET /api/tenants/{tenantKey}` |
| Create | `POST /api/tenants` |
| Activate | `POST /api/tenants/{tenantKey}/activate` |
| Suspend | `POST /api/tenants/{tenantKey}/suspend` |
| Delete | `DELETE /api/tenants/{tenantKey}` |

A tenant has a `tenantKey`, a `displayName`, and an active flag. Suspending a
tenant is the soft-off switch; workspaces carry a `tenantIsActive` flag that
reflects it.

> **Status:** The tenants screen is present but gated by a feature flag
> (`EnableTenantManagementApi`); if tenant management is disabled in your
> distribution the screen and its endpoints are not exposed.

## Users and members

There are two distinct concepts:

- **Users** (`/users`, Benutzer) are platform accounts. List, create, update,
  and delete them via `/api/users`; a user has an external id, optional email
  and display name, and a password. Operators see all users; a workspace-scoped
  caller sees only the members of their workspace.
- **Members** are users granted a role **inside a workspace**. You manage them
  from the workspace detail screen, not the user list — see
  [Workspaces & Surfaces](workspaces-surfaces.md).

A user can export their own data (`GET /api/users/{userId}/data-export`) to
support data-subject requests.

## Plugin management

Everything domain-specific is a plugin, so installing and activating plugins is a
core operator task. The Plugins screen (`/plugins`) lists installed plugins with
their state and signature standing. The lifecycle (API base `/api/plugins`):

| Step | Endpoint |
|---|---|
| Install (local build) | `POST /api/plugins/install/local` |
| Install (from NuGet) | `POST /api/plugins/install/nuget` |
| Activate | `POST /api/plugins/{pluginId}/activate` |
| Deactivate | `POST /api/plugins/{pluginId}/deactivate` |
| Uninstall | `DELETE /api/plugins/{pluginId}` |

A plugin moves through four states: **Installed**, **Active**, **Inactive**, and
**Uninstalled**. Install and activate are two separate steps — installing
registers the plugin; activating turns it on. Because the admin UI is loaded at
runtime from the plugin's manifest, installing, activating, and refreshing the
browser surfaces a plugin's admin screens **without a host restart**.

### Signing and trust

Installing a plugin is a fully privileged act: the plugin runs as host code and
its admin bundle as privileged admin-frontend code. The install gate verifies a
detached signature manifest (`plugin.signature.json`, ECDSA-P256) against a
**trusted signer's public key** and checks the covered file hashes, so
capabilities and entry type are tamper-evident.

- **Unsigned plugins are rejected** unless
  `BackendHost__AllowUnsignedPlugins=true`. The local dev stack sets this to
  `true`; production leaves it `false`, so every deployed plugin — including
  bundled system-tier plugins — must be signed and its signer trusted.
- Trust a signer by adding its public key under `BackendHost__TrustedSigners`.
- Revoke a compromised signer or a specific bad build via
  `BackendHost__RevokedSignerFingerprints` / `BackendHost__RevokedContentHashes`,
  enforced at install and, through runtime rehydration, at load.

The Plugins screen shows each plugin's signature state; a machine-readable report
is at `GET /api/plugins/signature-report`, and the trusted-signer list at
`GET /api/plugins/security/trusted-signers`. Lifecycle actions are recorded in an
audit log (`GET /api/plugins/audit`).

> Note the install headers when calling the API directly: authenticate with the
> API-key header (default `X-Callora-Api-Key`, configurable via
> `BackendHost__ApiKeyHeaderName`), whose accepted values are set in
> `BackendHost__ApiKeys`.

## System configuration

Plugins declare configuration keys; you set their values under `/config`
(Konfiguration). The model is scoped and resolves most-specific-first —
**workspace over tenant over global over default**:

- `GET /api/config/definitions` — the keys a plugin declares (optionally
  filtered by `pluginId`).
- `GET /api/config/effective?pluginId=...&workspaceKey=...` — the resolved
  values for a plugin in a scope. Secret-typed keys are masked as `***`.
- `PUT /api/config/values` — set values for a scope (`global`, `tenant`, or
  `workspace`). Global and tenant scopes are operator-only; workspace scope
  requires access to that workspace.

Next: [Workspaces & Surfaces](workspaces-surfaces.md).
