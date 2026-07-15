using Callora.Core.Application.Security;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Database-backed data-subject rights (PLAT-243). Erasure anonymizes the
/// append-only audit trail instead of deleting it: the events stay, the
/// person vanishes.
/// </summary>
public sealed class EfUserDataSubjectService(HostPersistenceDbContext dbContext) : IUserDataSubjectService
{
    private const string ErasedMarker = "erased-user";

    public async Task<UserDataExport?> ExportAsync(string externalId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return null;
        }

        var normalizedId = externalId.Trim();
        var user = await dbContext.BackendUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ExternalId == normalizedId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        var memberships = await dbContext.WorkspaceMemberships
            .AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .Select(x => new UserDataExportMembership(x.Workspace.WorkspaceKey, x.Role, x.AssignedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var role = await dbContext.BackendRbacUserRoles
            .AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .Select(x => x.Role.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var auditTrail = await dbContext.PluginAuditLogs
            .AsNoTracking()
            .Where(x => x.RequestedBy == normalizedId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Select(x => new UserDataExportAuditEntry(x.OccurredAtUtc, x.Action, x.PluginId, x.IsSuccess))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new UserDataExport(
            user.ExternalId,
            user.Email,
            user.DisplayName,
            user.CreatedAtUtc,
            role,
            memberships,
            auditTrail);
    }

    public async Task<bool> EraseAsync(string externalId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return false;
        }

        var normalizedId = externalId.Trim();
        var user = await dbContext.BackendUsers
            .SingleOrDefaultAsync(x => x.ExternalId == normalizedId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return false;
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await dbContext.PluginAuditLogs
            .Where(x => x.RequestedBy == normalizedId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.RequestedBy, ErasedMarker),
                cancellationToken)
            .ConfigureAwait(false);

        await dbContext.WorkspaceMemberships
            .Where(x => x.UserId == user.Id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await dbContext.BackendRbacUserRoles
            .Where(x => x.UserId == user.Id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        dbContext.BackendUsers.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
