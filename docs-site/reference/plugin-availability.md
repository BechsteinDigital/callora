# Plugin availability

A plugin can stop being available without being uninstalled: its entitlement lapses, a
capability it requires disappears, its runtime turns unhealthy, the workspace is suspended.
Callora calls the combination **effective availability**, derives it in one place, and every
serving path asks that one derivation rather than re-deciding for itself.

## Two questions, one derivation

Availability answers two different questions, and the difference is not a technicality:

- **Workspace availability** — *may this plugin serve workspace W?* Asked by requests,
  surfaces, MCP tools and workspace-scoped jobs and events.
- **Platform availability** — *may this plugin do any work on this host at all?* Asked where
  no workspace is named: platform-wide jobs and events, plugin-wide routes.

The second is not a relaxed version of the first. It is its **precondition**: the factors it
combines are exactly the ones that must hold in *every* workspace. A plugin that is
uninstalled, faulted, unentitled or over its fault budget is available nowhere. A plugin
activated in no workspace at all may still legitimately do platform-wide work.

## The factors

`PluginAvailability.From` combines them; a plugin is available exactly when **none** is unmet.

**Platform layer** (`PluginPlatformInputs`) — holds host-wide, and is the whole of a platform
verdict:

| Factor | Unmet when |
| --- | --- |
| `BundledOrInstalled` | The plugin is not installed on this host |
| `RuntimeHealthy` | Its runtime is faulted |
| `Entitled` | No entitlement covers it — see the precedence below |
| `WithinFaultBudget` | It exceeded its fault budget (`PluginFaultRegistry`) |

**Workspace layer** (`PluginWorkspaceInputs`) — only exists relative to one workspace, and is
added on top for a workspace verdict:

| Factor | Unmet when |
| --- | --- |
| `WorkspaceEnabled` | The workspace has not activated it |
| `TenantActive` | The owning tenant is suspended |
| `WorkspaceActive` | The workspace is suspended |
| `RequiredCapabilitiesAvailable` | A capability from `requiresCapabilities` is not provided there |

The layers are separate types on purpose. A platform verdict that claims `WorkspaceEnabled` is
not merely discouraged — it is unconstructible, because the field does not exist on its input
type. `UnmetFactors` therefore stays exact: a platform verdict names only factors it observed.

Consumers reach both through `IPluginAvailabilityEvaluator` (`EvaluateAsync`,
`EvaluatePlatformAsync`) — never by re-implementing the combination.

### How entitlement resolves

`EfPluginEntitlementStore` uses a fixed precedence:

**workspace row → tenant row → platform row → `BackendHost:DefaultPluginEntitlement`**

A platform row carries neither `WorkspaceKey` nor `TenantKey`. The configured default is
policy: `true` suits self-hosted installs where every installed plugin is usable, `false`
suits cloud and marketplace deployments where every grant is explicit.

::: warning The platform verdict asks on the default tenant
`EvaluatePlatformAsync` queries with `tenantKey = BackendHost:DefaultTenantKey`, not with no
tenant at all. `MarketplaceEntitlementApplier` writes a **tenant** row for a workspace-less
grant, never a platform row; asking without a tenant would skip that row by the precedence
above and fall through to the default. On a marketplace deployment that default is `false`,
so a paid plugin would sit idle. Without a configured `DefaultTenantKey` the query falls back
to the platform row and then the default.
:::

::: info Entitlement is derived, not written
A lapse does **not** deactivate the plugin. `MarketplaceEntitlementApplier` records the event
and writes the entitlement store; the workspace's desired activation is left alone. So a
billing outage makes a plugin dark, and restoring the entitlement makes it serve again with
no reconfiguration.
:::

## What enforces it

| Entry point | Where | Question asked |
| --- | --- | --- |
| Plugin HTTP routes, workspace-scoped | `PluginApiEndpointDataSource` | Workspace |
| Plugin HTTP routes, plugin-wide | `PluginApiEndpointDataSource` | Platform |
| Plugin Admin-API extension routes | `PluginAdminExtensionEndpoints` | Workspace |
| Plugin surface API routes | `PluginSurfaceApiEndpoints` | Workspace |
| MCP tools contributed by plugins | `ContributedMcpTool` | Workspace |
| Surface slots and the UI chain | `SurfaceSlotResolver`, `WorkspaceUiChainResolver` | Workspace |
| Surface identity | `SurfaceIdentityResolver`, `SurfaceIdentityAssignmentService` | Workspace |
| Background jobs | `BackgroundJobProcessor` | Workspace, or platform when the job carries no `WorkspaceKey` |
| Business events | `BusinessEventBus` | Workspace, or platform when the event carries no `WorkspaceKey` |
| Host events | `HostApplicationEventDispatcher` | Workspace, or platform when the event carries no `WorkspaceKey` |

### What callers see

- **HTTP routes** answer `403` with a problem document naming the plugin, and saying whether
  it is unavailable *in this workspace* or *on this host*.
- **Background jobs** are **parked**, not failed: the attempt is given back and the job is
  rescheduled after `BackgroundJobs:UnavailableRetryDelay`. Failing them would let a billing
  outage burn the retry budget and destroy the work.
- **Events** are withheld. This matters beyond observation: `MutableBusinessEvent` and
  `MutableHostEvent` let a listener **veto** a host operation, so an unavailable plugin must
  not be consulted at all — otherwise a plugin the workspace no longer holds could keep
  blocking operations.
- **Surfaces and slots** render without the plugin's contribution.

## What does not enforce it

| Not gated | Why |
| --- | --- |
| Host-owned handlers, listeners and subscribers | They have no owning plugin, so no entitlement can lapse for them |

That is the whole list, and it is a definition rather than a gap: the gate keys on the export's
owning plugin, and host rails have none.

## When the gate is missing

`AddCalloraHost` always registers `IPluginAvailabilityEvaluator`. A host composing the core by
hand without it is a broken host, not a minimal one — workspace-scoped plugin routes answer
**`503 Service Unavailable`**, not `403`. The distinction is deliberate: the host cannot
answer the question, which is a different fact from the caller not being allowed, and a `403`
would send an operator hunting an entitlement problem that does not exist.

For the same reason `IPluginAvailabilityEvaluator.EvaluatePlatformAsync` has a default
implementation that **refuses**. An evaluator that has not implemented the platform question
must not answer it with a workspace answer, and returning "available" would open exactly the
gate the abstraction exists to close.
