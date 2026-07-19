# Capabilities & Entitlements

Callora has two related-but-distinct gating systems:

- **Capabilities** are a *technical* dependency graph — "this plugin needs that plugin's
  feature to work at all". They are declared in the manifest and enforced by the lifecycle.
- **Entitlements** are a *commercial/feature* gate — "this plugin is licensed for this
  workspace/tenant". They carry provenance (who granted them) and feed a plugin's
  effective availability.

## Capabilities

A plugin declares what it **provides** and what it **requires** in `registry.json`:

```json
{
  "pluginId": "dialer",
  "capabilities": [],
  "requiresCapabilities": ["communication.voice"],
  "dependencies": { "Callora.Plugin.Communication.Abstractions": ">=0.1.0" }
}
```

- `capabilities` — capability ids this plugin provides (e.g. Communication provides
  `communication.voice`).
- `requiresCapabilities` — capability ids this plugin needs from other active plugins.

The manifest values are stored on the installation aggregate (`PluginInstallation`,
`src/Core/Domain/Plugins/`) as `ProvidedCapabilities` / `RequiredCapabilities`, encoded via
`CapabilityListCodec`. Before an activate or deactivate, **`PluginCapabilityGuard`**
(`src/Core/Application/Lifecycle/`) checks the graph: you cannot activate a plugin whose
required capabilities are not provided by an active plugin, and you cannot deactivate a
plugin another active plugin depends on. Capabilities are checkable both globally and per
workspace.

Use a capability when a plugin genuinely cannot function without another's feature. It is a
hard dependency, not a soft preference.

## Entitlements

An entitlement records whether a plugin is licensed for a scope. The model
(`PluginEntitlement`, `src/Core/Domain/Entitlements/`) is deliberately small:

```csharp
public sealed class PluginEntitlement
{
    public string PluginId { get; set; }
    public string? TenantKey { get; set; }     // null = platform-wide
    public string? WorkspaceKey { get; set; }  // null = tenant-wide
    public bool IsEntitled { get; set; }
    public string Source { get; set; }          // provenance — see below
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

Entitlements are **scoped**: a `WorkspaceKey`-specific record is more specific than a
`TenantKey`-wide one, which is more specific than a platform-wide one. The store
(`IPluginEntitlementStore.IsEntitledAsync`, `src/Core/Application/Entitlements/`) resolves
the most specific applicable record for a `(pluginId, tenant, workspace)` query.

### Provenance — manual vs. marketplace

The `Source` field records **who** made the decision. The two first-class values are:

| `Source` | Meaning |
| --- | --- |
| `"manual"` | A direct operator grant/revoke — the default. |
| `"marketplace"` | An inbound marketplace-sync decision. |

`"migrated"` also appears for records carried over from earlier state. **Last writer wins**:
a later grant/revoke — from either source — overwrites the record and updates `Source`, so
provenance always reflects the most recent decision. Marketplace decisions arrive as
idempotent sync events (`MarketplaceEntitlementEventRecord` keyed by external `EventId`)
processed by `MarketplaceEntitlementSyncJobHandler`
(`src/Core/Application/Entitlements/`), with actions `grant` and `revoke`.

## Effective availability

A plugin being installed and entitled is **necessary but not sufficient** for it to actually
serve a workspace. The real "effectively available" decision is derived by
**`IPluginAvailabilityEvaluator`** (`src/Core/Application/Plugins/`), which combines several
factors (`PluginAvailabilityFactor`):

```csharp
public enum PluginAvailabilityFactor
{
    BundledOrInstalled,
    RuntimeHealthy,
    Entitled,
    WorkspaceEnabled,
    TenantActive,
    WorkspaceActive,
    RequiredCapabilitiesAvailable,
}
```

A plugin is available in a workspace only when **all** factors hold: it is installed and its
runtime is healthy, it is **entitled** for that scope, the workspace has it enabled, the
tenant and workspace are active, and its **required capabilities** are satisfied. This is why
entitlements and capabilities are documented together — they are two inputs to the same
availability derivation.

The evaluation gates real behavior: a `WorkspaceApiController` route
([Backend Extensions](backend-extensions.md#plugin-controllers-and-dynamic-routing)) returns
`403` when the plugin is not effectively available in the target workspace, and the surface
UI chain omits an unavailable plugin's views. Losing an entitlement, deactivating a
capability provider, or deactivating the workspace all remove the plugin from that
workspace's surface without any code change.

## Practical guidance

- Declare `requiresCapabilities` for **technical** hard dependencies; the lifecycle guard
  enforces them and gives clear activation errors.
- Do **not** use capabilities for licensing — that is what entitlements are for.
- Never check entitlement state yourself and cache it; rely on the availability evaluator
  and the `WorkspaceApiController` gate so a revoked entitlement takes effect immediately.
