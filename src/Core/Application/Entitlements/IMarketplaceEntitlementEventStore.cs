using Callora.Core.Domain.Entitlements;

namespace Callora.Core.Application.Entitlements;

/// <summary>
/// Persistence port for processed marketplace entitlement events.
/// </summary>
public interface IMarketplaceEntitlementEventStore
{
    /// <summary>
    /// Records one processed event. Returns false when the event id was
    /// already processed (idempotent replay).
    /// </summary>
    Task<bool> TryRecordAsync(MarketplaceEntitlementEventRecord record, CancellationToken cancellationToken = default);
}
