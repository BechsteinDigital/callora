using Callora.Core.Application.Surfaces;
using Callora.Core.Domain.Surfaces;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Relational store for authenticated surface sessions (ADR-017 §8.1). Storing them
/// is what makes immediate, server-side revocation possible — a self-contained token
/// could not be withdrawn before its own expiry.
/// </summary>
public sealed class EfSurfaceSessionStore(HostPersistenceDbContext dbContext) : ISurfaceSessionStore
{
    public async Task<SurfaceSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            return null;
        }

        var record = await dbContext.SurfaceSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : ToSession(record);
    }

    public async Task CreateAsync(SurfaceSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        dbContext.SurfaceSessions.Add(new SurfaceSessionRecord
        {
            Id = session.SessionId,
            TenantKey = session.TenantKey,
            WorkspaceKey = session.WorkspaceKey,
            SurfaceKey = session.SurfaceKey,
            Audience = session.Audience,
            Issuer = session.Subject.Issuer,
            SubjectId = session.Subject.SubjectId,
            DisplayName = session.Identity.DisplayName,
            ClaimsJson = SurfaceSessionClaimsSerializer.Serialize(session.Identity.Claims),
            AuthenticationMethod = session.Identity.AuthenticationMethod,
            AuthenticatedAtUtc = session.Identity.AuthenticatedAtUtc,
            IssuedAtUtc = session.IssuedAtUtc,
            ExpiresAtUtc = session.ExpiresAtUtc,
            LastSeenAtUtc = session.IssuedAtUtc,
            IdentityPluginId = session.IdentityPluginId,
            IdentityVersion = session.IdentityVersion,
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task TouchAsync(
        Guid sessionId,
        DateTimeOffset seenAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            return;
        }

        // Deliberately does not extend the expiry: a session lives exactly as long as
        // the identity that was vouched for, no matter how busy the visitor is.
        await dbContext.SurfaceSessions
            .Where(x => x.Id == sessionId)
            .ExecuteUpdateAsync(
                x => x.SetProperty(p => p.LastSeenAtUtc, seenAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            return;
        }

        await dbContext.SurfaceSessions
            .Where(x => x.Id == sessionId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> RevokeForSurfaceAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(surfaceKey))
        {
            return 0;
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        var normalizedSurfaceKey = surfaceKey.Trim();
        return await dbContext.SurfaceSessions
            .Where(x => x.WorkspaceKey == normalizedWorkspaceKey && x.SurfaceKey == normalizedSurfaceKey)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> PurgeExpiredAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default) =>
        await dbContext.SurfaceSessions
            .Where(x => x.ExpiresAtUtc <= nowUtc)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    private static SurfaceSession ToSession(SurfaceSessionRecord record) =>
        new(
            record.Id,
            record.TenantKey,
            record.WorkspaceKey,
            record.SurfaceKey,
            record.Audience,
            new SurfaceSubject(record.Issuer, record.SubjectId),
            new SurfaceIdentity(
                record.DisplayName,
                SurfaceSessionClaimsSerializer.Deserialize(record.ClaimsJson),
                record.AuthenticationMethod,
                record.AuthenticatedAtUtc,
                record.ExpiresAtUtc),
            record.IssuedAtUtc,
            record.ExpiresAtUtc,
            record.IdentityPluginId,
            record.IdentityVersion);
}
