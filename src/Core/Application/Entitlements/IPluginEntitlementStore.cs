using Callora.Core.Application.Entitlements;

namespace Callora.Core.Application.Entitlements;

public interface IPluginEntitlementStore
{
    ValueTask<bool> IsEntitledAsync(
        string pluginId,
        string? workspaceKey = null,
        string? tenantKey = null,
        CancellationToken cancellationToken = default);

    /// <param name="source">Provenance of the decision, recorded on the row —
    /// e.g. "manual" for a direct operator grant/revoke (the default) or
    /// "marketplace" for the inbound sync. The last writer wins.</param>
    ValueTask SetEntitledAsync(
        string pluginId,
        bool isEntitled,
        string? workspaceKey = null,
        string? tenantKey = null,
        string source = "manual",
        CancellationToken cancellationToken = default);

    ValueTask ClearForPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every recorded entitlement decision across all scopes, for operator
    /// review. This enumerates the explicit rows only — the configured
    /// <see cref="Callora.Core.Application.Policies.BackendHostOptions.DefaultPluginEntitlement"/>
    /// fallback that applies where no row exists is not materialised here.
    /// </summary>
    ValueTask<IReadOnlyList<PluginEntitlementSnapshot>> ListAsync(
        CancellationToken cancellationToken = default);
}
