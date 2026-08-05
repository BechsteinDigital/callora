using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Relational store for resume promises (ADR-018 §2.2). Redemption deletes the row and returns what
/// it deleted, so single use is enforced by the database rather than by a check the caller could
/// race.
/// </summary>
public sealed class EfSessionResumeTicketStore(HostPersistenceDbContext dbContext) : ISessionResumeTicketStore
{
    public async Task CreateAsync(
        SessionResumeTicketRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        dbContext.SessionResumeTickets.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SessionResumeTicketRecord?> ConsumeAsync(
        string tokenHash,
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash) || string.IsNullOrWhiteSpace(pluginId))
        {
            return null;
        }

        // Read then delete-by-id: the delete's affected-row count is what decides the race, so two
        // concurrent redemptions cannot both walk away with the ticket.
        var record = await dbContext.SessionResumeTickets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.TokenHash == tokenHash && x.PluginId == pluginId,
                cancellationToken)
            .ConfigureAwait(false);
        if (record is null)
        {
            return null;
        }

        var deleted = await dbContext.SessionResumeTickets
            .Where(x => x.Id == record.Id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return deleted == 1 ? record : null;
    }

    public async Task<bool> DeleteAsync(
        string tokenHash,
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash) || string.IsNullOrWhiteSpace(pluginId))
        {
            return false;
        }

        var deleted = await dbContext.SessionResumeTickets
            .Where(x => x.TokenHash == tokenHash && x.PluginId == pluginId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return deleted > 0;
    }

    public async Task<int> PurgeExpiredAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default) =>
        await dbContext.SessionResumeTickets
            .Where(x => x.ExpiresAtUtc <= nowUtc)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
}
