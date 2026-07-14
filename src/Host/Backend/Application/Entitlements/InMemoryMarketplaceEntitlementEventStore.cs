using System.Collections.Concurrent;
using Callora.Host.Backend.Application.Entitlements;
using Callora.Host.Backend.Domain.Entitlements;

namespace Callora.Host.Backend.Application.Entitlements;

/// <summary>
/// Thread-safe in-memory idempotency store for tests and hosts without database.
/// </summary>
public sealed class InMemoryMarketplaceEntitlementEventStore : IMarketplaceEntitlementEventStore
{
    private readonly ConcurrentDictionary<string, MarketplaceEntitlementEventRecord> _records =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> TryRecordAsync(
        MarketplaceEntitlementEventRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return Task.FromResult(_records.TryAdd(record.EventId, record));
    }
}
