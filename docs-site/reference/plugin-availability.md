# Plugin availability

A plugin can stop being available without being uninstalled: its entitlement lapses, a
capability it requires disappears, its runtime turns unhealthy, the workspace is suspended.
Callora calls the combination **effective availability**, derives it in one place, and every
serving path asks that one derivation rather than re-deciding for itself.

This page lists **which entry points enforce it**, which deliberately do not, and what an
unavailable plugin's callers actually see.

## The derivation

`PluginAvailability.From` (`src/Core/Application/Plugins/`) combines eight factors. A plugin is
available in a workspace exactly when **none** is unmet:

| Factor | Unmet when |
| --- | --- |
| `BundledOrInstalled` | The plugin is not installed on this host |
| `RuntimeHealthy` | Its runtime is faulted |
| `Entitled` | No entitlement covers it for this workspace or tenant |
| `WorkspaceEnabled` | The workspace has not activated it |
| `TenantActive` | The owning tenant is suspended |
| `WorkspaceActive` | The workspace is suspended |
| `RequiredCapabilitiesAvailable` | A capability it declares in `requiresCapabilities` is not provided |
| `WithinFaultBudget` | It exceeded its fault budget (see `PluginFaultRegistry`) |

Consumers reach it through `IPluginAvailabilityEvaluator` — never by re-implementing the
combination.

::: info Entitlement is derived, not written
A lapse does **not** deactivate the plugin. `MarketplaceEntitlementApplier` records the event
and writes the entitlement store; the workspace's desired activation is left alone. So a
billing outage makes a plugin dark, and restoring the entitlement makes it serve again with
no reconfiguration.
:::

## What enforces it

| Entry point | Where | Workspace comes from |
| --- | --- | --- |
| Plugin HTTP routes (workspace-scoped) | `PluginApiEndpointDataSource` | The request |
| Plugin Admin-API extension routes | `PluginAdminExtensionEndpoints` | The request |
| Plugin surface API routes | `PluginSurfaceApiEndpoints` | The request |
| MCP tools contributed by plugins | `ContributedMcpTool` | The call scope |
| Surface slots and the UI chain | `SurfaceSlotResolver`, `WorkspaceUiChainResolver` | The surface |
| Surface identity | `SurfaceIdentityResolver`, `SurfaceIdentityAssignmentService` | The surface |
| **Background jobs** | `BackgroundJobProcessor` | `BackgroundJob.WorkspaceKey` |
| **Business events** | `BusinessEventBus` | `IBusinessEvent.WorkspaceKey` |
| **Host events** | `HostApplicationEventDispatcher` | `IBusinessEvent.WorkspaceKey` |

### What callers see

- **HTTP routes** answer `403` with a problem document naming the plugin.
- **Background jobs** are **parked**, not failed: the attempt is given back and the job is
  rescheduled after `BackgroundJobs:UnavailableRetryDelay`. Failing them would let a billing
  outage burn the retry budget and destroy the work.
- **Events** are withheld. This matters beyond observation: `MutableBusinessEvent` and
  `MutableHostEvent` let a listener **veto** a host operation, so an unavailable plugin must
  not be consulted at all — otherwise a plugin the workspace no longer holds could keep
  blocking operations.
- **Surfaces and slots** render without the plugin's contribution.

## What deliberately does not enforce it

Availability is derived **per workspace**. An entry point that names no workspace is not
asking a question the derivation can answer, and these therefore stay ungated:

| Not gated | Why |
| --- | --- |
| Background jobs with no `WorkspaceKey` | Platform-wide work; failing them closed would break every platform-wide plugin job |
| Business and host events with no `WorkspaceKey` | Platform-wide events; the same |
| Plugin routes declared `HostAdminApiRouteScope.Global` | An explicit opt-out for plugin-wide status and metadata |
| Host-owned handlers, listeners and subscribers | They have no owning plugin, so no entitlement can lapse for them |

### Why they are not simply gated too

Not because a platform-wide entitlement is undefined — it is defined and stored.
`EfPluginEntitlementStore` resolves in a fixed precedence: **workspace row → tenant row →
platform row → `BackendHost:DefaultPluginEntitlement`**. A platform row is one with neither
`WorkspaceKey` nor `TenantKey`, and the default is deliberate policy: `true` suits self-hosted
installs where every installed plugin is usable, `false` suits cloud and marketplace
deployments where every grant is explicit.

What is missing is the **derivation** without a workspace, because the eight factors split
unevenly:

| Determinable without a workspace | Requires a workspace |
| --- | --- |
| `BundledOrInstalled` — the installation repository is global | `WorkspaceEnabled` — reads that workspace's activations |
| `RuntimeHealthy` — host lifecycle state, global by design | `TenantActive` / `WorkspaceActive` — properties of that workspace |
| `Entitled` — falls back to the platform row | `RequiredCapabilitiesAvailable` — checked against that workspace's active set |
| `WithinFaultBudget` — counted per plugin, not per workspace | |

So a platform-wide verdict is answerable — it means *may this plugin do any work on this host
at all* — but it is a **different question** from workspace availability, with four factors
instead of eight. `IPluginAvailabilityEvaluator.EvaluateAsync` takes a non-null `workspaceKey`
and cannot express it, which is why these entry points stay ungated for now rather than
because the concept is unsettled.

## When the gate is missing

`AddCalloraHost` always registers `IPluginAvailabilityEvaluator`. A host composing the core by
hand without it is a broken host, not a minimal one — workspace-scoped plugin routes answer
**`503 Service Unavailable`**, not `403`. The distinction is deliberate: the host cannot
answer the question, which is a different fact from the caller not being allowed, and a `403`
would send an operator hunting an entitlement problem that does not exist.
