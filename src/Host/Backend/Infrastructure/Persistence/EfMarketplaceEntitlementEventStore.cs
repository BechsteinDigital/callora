using Callora.Host.Backend.Application.Abstractions.Entitlements;
using Callora.Host.Backend.Domain.Entitlements;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL-backed idempotency store for marketplace entitlement events.
/// </summary>
public sealed class EfMarketplaceEntitlementEventStore(HostPersistenceDbContext dbContext)
    : IMarketplaceEntitlementEventStore
{
    public async Task<bool> TryRecordAsync(
        MarketplaceEntitlementEventRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var alreadyProcessed = await dbContext.MarketplaceEntitlementEvents
            .AsNoTracking()
            .AnyAsync(x => x.EventId == record.EventId, cancellationToken)
            .ConfigureAwait(false);
        if (alreadyProcessed)
            return false;

        dbContext.MarketplaceEntitlementEvents.Add(record);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            // Unique-Index-Kollision: Ein paralleler Prozess hat dasselbe Event bereits verbucht.
            dbContext.Entry(record).State = EntityState.Detached;
            return false;
        }
    }
}
