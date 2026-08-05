using Callora.Core.Application.Surfaces;
using Callora.Core.Domain.Surfaces;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Relational store for one-time handoff tickets (ADR-017 §8.4). Redemption deletes
/// the row and returns what it deleted, so single use is enforced by the database
/// rather than by a check the caller could race.
/// </summary>
public sealed class EfSurfaceHandoffTicketStore(HostPersistenceDbContext dbContext) : ISurfaceHandoffTicketStore
{
    public async Task CreateAsync(
        SurfaceHandoffTicket ticket,
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        dbContext.SurfaceHandoffTickets.Add(new SurfaceHandoffTicketRecord
        {
            Id = ticket.TicketId,
            TokenHash = tokenHash,
            TenantKey = ticket.TenantKey,
            WorkspaceKey = ticket.WorkspaceKey,
            SourceSurfaceKey = ticket.SourceSurfaceKey,
            TargetSurfaceKey = ticket.TargetSurfaceKey,
            TargetAudience = ticket.TargetAudience,
            Issuer = ticket.Subject.Issuer,
            SubjectId = ticket.Subject.SubjectId,
            DisplayName = ticket.Identity.DisplayName,
            ClaimsJson = SurfaceSessionClaimsSerializer.Serialize(ticket.Identity.Claims),
            AuthenticationMethod = ticket.Identity.AuthenticationMethod,
            AuthenticatedAtUtc = ticket.Identity.AuthenticatedAtUtc,
            IdentityExpiresAtUtc = ticket.Identity.ExpiresAtUtc,
            IssuedAtUtc = ticket.IssuedAtUtc,
            ExpiresAtUtc = ticket.ExpiresAtUtc,
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SurfaceHandoffTicket?> ConsumeAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return null;
        }

        // Read then delete-by-id: the delete's affected-row count is what decides the
        // race, so two concurrent redemptions cannot both walk away with the ticket.
        var record = await dbContext.SurfaceHandoffTickets
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);
        if (record is null)
        {
            return null;
        }

        var deleted = await dbContext.SurfaceHandoffTickets
            .Where(x => x.Id == record.Id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return deleted == 1 ? ToTicket(record) : null;
    }

    public async Task<int> PurgeExpiredAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default) =>
        await dbContext.SurfaceHandoffTickets
            .Where(x => x.ExpiresAtUtc <= nowUtc)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    private static SurfaceHandoffTicket ToTicket(SurfaceHandoffTicketRecord record) =>
        new(
            record.Id,
            record.TenantKey,
            record.WorkspaceKey,
            record.SourceSurfaceKey,
            record.TargetSurfaceKey,
            record.TargetAudience,
            new SurfaceSubject(record.Issuer, record.SubjectId),
            new SurfaceIdentity(
                record.DisplayName,
                SurfaceSessionClaimsSerializer.Deserialize(record.ClaimsJson),
                record.AuthenticationMethod,
                record.AuthenticatedAtUtc,
                record.IdentityExpiresAtUtc),
            record.IssuedAtUtc,
            record.ExpiresAtUtc);
}
