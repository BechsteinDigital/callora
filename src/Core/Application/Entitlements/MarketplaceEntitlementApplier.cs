using Callora.Core.Application.Audit;
using Callora.Core.Application.Entitlements;
using Callora.Core.Domain.Entitlements;

namespace Callora.Core.Application.Entitlements;

/// <summary>
/// Applies one marketplace entitlement event to the local entitlement store.
/// Replays of the same event id are skipped (idempotent).
/// </summary>
public sealed class MarketplaceEntitlementApplier(
    IMarketplaceEntitlementEventStore eventStore,
    IPluginEntitlementStore entitlementStore,
    IHostAuditStore auditStore)
{
    /// <summary>
    /// Applies one event. Returns false when the event was already processed.
    /// </summary>
    public async Task<bool> ApplyAsync(
        MarketplaceEntitlementEventPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!MarketplaceEntitlementActions.IsSupported(payload.Action))
        {
            throw new InvalidOperationException($"Unsupported marketplace entitlement action '{payload.Action}'.");
        }

        var record = MarketplaceEntitlementEventRecord.Create(
            payload.EventId,
            payload.Action,
            payload.PluginId,
            payload.TenantKey,
            payload.WorkspaceKey,
            DateTimeOffset.UtcNow);

        var isFirstProcessing = await eventStore.TryRecordAsync(record, cancellationToken).ConfigureAwait(false);
        if (!isFirstProcessing)
        {
            return false;
        }

        var isGrant = string.Equals(payload.Action, MarketplaceEntitlementActions.Grant, StringComparison.OrdinalIgnoreCase);
        await entitlementStore
            .SetEntitledAsync(payload.PluginId, isGrant, payload.WorkspaceKey, payload.TenantKey, "marketplace", cancellationToken)
            .ConfigureAwait(false);

        await auditStore.WritePluginAuditAsync(
                action: "entitlement.marketplace-sync",
                pluginId: payload.PluginId,
                isSuccess: true,
                requestedBy: "marketplace",
                message: $"Marketplace event '{payload.EventId}' applied: {payload.Action}.",
                metadata: new Dictionary<string, string>
                {
                    ["eventId"] = payload.EventId,
                    ["action"] = payload.Action,
                    ["tenantKey"] = payload.TenantKey,
                    ["workspaceKey"] = payload.WorkspaceKey ?? string.Empty
                },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
