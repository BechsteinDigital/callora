using Callora.Core.Application.Security;
using Callora.Core.Domain.Security;
using Callora.Core.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class EfBackendRbacStore(HostPersistenceDbContext dbContext) : IBackendRbacStore
{
    public async Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> GetRolePermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        var roles = await dbContext.BackendRbacRoles
            .AsNoTracking()
            .Include(x => x.Permissions)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byRole = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            byRole[role.Name] = role.Permissions
                .Select(x => x.PermissionKey)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
        }

        return byRole;
    }

    public async Task<IReadOnlyCollection<string>?> GetRolePermissionsAsync(
        string role,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        var entity = await dbContext.BackendRbacRoles
            .AsNoTracking()
            .Include(x => x.Permissions)
            .SingleOrDefaultAsync(x => x.Name == role.Trim(), cancellationToken)
            .ConfigureAwait(false);

        return entity?.Permissions.Select(x => x.PermissionKey).ToArray();
    }

    public async Task UpsertRoleAsync(
        string role,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentNullException.ThrowIfNull(permissions);

        var roleName = role.Trim();
        var entity = await dbContext.BackendRbacRoles
            .Include(x => x.Permissions)
            .SingleOrDefaultAsync(x => x.Name == roleName, cancellationToken)
            .ConfigureAwait(false);

        if (entity is { IsSystem: true })
            throw new InvalidOperationException($"Role '{roleName}' is fixed and cannot be modified.");

        if (entity is null)
        {
            entity = new BackendRbacRole
            {
                Id = Guid.NewGuid(),
                Name = roleName,
                IsSystem = false,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            dbContext.BackendRbacRoles.Add(entity);
        }

        var normalizedPermissions = NormalizePermissions(permissions, roleName);
        var current = new HashSet<string>(entity.Permissions.Select(x => x.PermissionKey), StringComparer.Ordinal);

        foreach (var permission in current)
        {
            if (!normalizedPermissions.Contains(permission))
            {
                var toRemove = entity.Permissions.Single(x => x.PermissionKey == permission);
                dbContext.BackendRbacRoleGrants.Remove(toRemove);
            }
        }

        foreach (var permission in normalizedPermissions)
        {
            if (!current.Contains(permission))
            {
                entity.Permissions.Add(new BackendRbacRoleGrant
                {
                    Id = Guid.NewGuid(),
                    RoleId = entity.Id,
                    PermissionKey = permission
                });
            }
        }

        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RemoveRoleAsync(
        string role,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        var entity = await dbContext.BackendRbacRoles
            .SingleOrDefaultAsync(x => x.Name == role.Trim(), cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
            return false;

        if (entity.IsSystem)
            return false;

        dbContext.BackendRbacRoles.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetUserRolesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.BackendRbacUserRoles
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Role)
            .OrderBy(x => x.User.ExternalId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(x => x.User.ExternalId, x => x.Role.Name, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string?> GetUserRoleAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var row = await dbContext.BackendRbacUserRoles
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Role)
            .SingleOrDefaultAsync(x => x.User.ExternalId == userId.Trim(), cancellationToken)
            .ConfigureAwait(false);

        return row?.Role.Name;
    }

    public async Task UpsertUserRoleAsync(
        string userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        var roleEntity = await dbContext.BackendRbacRoles
            .SingleOrDefaultAsync(x => x.Name == role.Trim(), cancellationToken)
            .ConfigureAwait(false);
        if (roleEntity is null)
            throw new InvalidOperationException($"Role '{role}' is not defined.");

        var normalizedUserId = userId.Trim();
        var userEntity = await dbContext.BackendUsers
            .SingleOrDefaultAsync(x => x.ExternalId == normalizedUserId, cancellationToken)
            .ConfigureAwait(false);
        if (userEntity is null)
        {
            userEntity = new BackendUser
            {
                Id = Guid.NewGuid(),
                ExternalId = normalizedUserId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            dbContext.BackendUsers.Add(userEntity);
        }
        else
        {
            userEntity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        var userRole = await dbContext.BackendRbacUserRoles
            .SingleOrDefaultAsync(x => x.UserId == userEntity.Id, cancellationToken)
            .ConfigureAwait(false);

        if (userRole is null)
        {
            userRole = new BackendRbacUserRole
            {
                Id = Guid.NewGuid(),
                UserId = userEntity.Id,
                RoleId = roleEntity.Id,
                AssignedAtUtc = DateTimeOffset.UtcNow
            };
            dbContext.BackendRbacUserRoles.Add(userRole);
        }
        else
        {
            userRole.RoleId = roleEntity.Id;
            userRole.AssignedAtUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RemoveUserRoleAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var normalizedUserId = userId.Trim();
        var userEntity = await dbContext.BackendUsers
            .SingleOrDefaultAsync(x => x.ExternalId == normalizedUserId, cancellationToken)
            .ConfigureAwait(false);
        if (userEntity is null)
            return false;

        var row = await dbContext.BackendRbacUserRoles
            .SingleOrDefaultAsync(x => x.UserId == userEntity.Id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return false;

        dbContext.BackendRbacUserRoles.Remove(row);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static HashSet<string> NormalizePermissions(IReadOnlyCollection<string> permissions, string role)
    {
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var permission in permissions)
        {
            var trimmed = permission.Trim().ToLowerInvariant();
            if (!BackendPermissionKeyValidator.IsValid(trimmed))
                throw new InvalidOperationException($"Permission '{permission}' is invalid for role '{role}'.");

            normalized.Add(trimmed);
        }

        return normalized;
    }
}
