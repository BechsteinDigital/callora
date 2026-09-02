using Callora.Core.Application.Security;
using Callora.Core.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

/// <inheritdoc />
public sealed class EfWorkspaceMembershipRoleStore(HostPersistenceDbContext dbContext)
    : IWorkspaceMembershipRoleStore
{
    private readonly HostPersistenceDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListRolesAsync(
        string workspaceKey, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        return await Assignments(workspaceKey, userId)
            .Select(assignment => assignment.Role.Name)
            .OrderBy(name => name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>?> ReplaceRolesAsync(
        string workspaceKey,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roles);

        var membership = await MembershipAsync(workspaceKey, userId, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return null;
        }

        var wanted = roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Nur Rollen, die es gibt. Eine Zuweisung auf einen Namen, den niemand angelegt hat, wäre eine
        // Zeile, die nichts bewirkt und in der Oberfläche aussieht, als bewirke sie etwas.
        var resolved = await _dbContext.BackendRbacRoles
            .Where(role => wanted.Contains(role.Name))
            .Select(role => new { role.Id, role.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existing = await _dbContext.WorkspaceMembershipRoles
            .Where(assignment => assignment.MembershipId == membership.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var keep = resolved.Select(role => role.Id).ToHashSet();
        _dbContext.WorkspaceMembershipRoles.RemoveRange(
            existing.Where(assignment => !keep.Contains(assignment.RoleId)));

        var already = existing.Select(assignment => assignment.RoleId).ToHashSet();
        foreach (var role in resolved.Where(role => !already.Contains(role.Id)))
        {
            _dbContext.WorkspaceMembershipRoles.Add(new WorkspaceMembershipRole
            {
                Id = Guid.NewGuid(),
                MembershipId = membership.Id,
                RoleId = role.Id,
                AssignedAtUtc = DateTimeOffset.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return [.. resolved.Select(role => role.Name).OrderBy(name => name, StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListUsersWithRoleAsync(
        string role, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return [];
        }

        var trimmed = role.Trim();

        return await _dbContext.WorkspaceMembershipRoles
            .Where(assignment => assignment.Role.Name == trimmed)
            .Select(assignment => assignment.Membership.User.ExternalId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private IQueryable<WorkspaceMembershipRole> Assignments(string workspaceKey, string userId)
    {
        var key = workspaceKey.Trim();
        var external = userId.Trim();

        return _dbContext.WorkspaceMembershipRoles
            .Where(assignment => assignment.Membership.Workspace.WorkspaceKey == key
                && assignment.Membership.User.ExternalId == external);
    }

    private async Task<WorkspaceMembership?> MembershipAsync(
        string workspaceKey, string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var key = workspaceKey.Trim();
        var external = userId.Trim();

        return await _dbContext.WorkspaceMemberships
            .FirstOrDefaultAsync(
                membership => membership.Workspace.WorkspaceKey == key && membership.User.ExternalId == external,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
