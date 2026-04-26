using System.Linq.Expressions;
using Callora.Host.Backend.Application.Abstractions.Workspaces;
using Callora.Host.Backend.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using WorkspaceEntity = Callora.Host.Backend.Domain.Workspaces.Workspace;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class EfWorkspaceManagementStore(HostPersistenceDbContext dbContext) : IWorkspaceManagementStore
{
    public async Task<IReadOnlyList<WorkspaceSnapshot>> ListAsync(
        string? tenantKey = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Workspaces
            .AsNoTracking()
            .Include(x => x.Tenant)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(tenantKey))
        {
            var normalizedTenantKey = tenantKey.Trim();
            query = query.Where(x => x.Tenant.TenantKey == normalizedTenantKey);
        }

        return await query
            .OrderBy(x => x.WorkspaceKey)
            .Select(ToSnapshotExpressionWithTenant())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<WorkspaceSnapshot?> GetAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Task.FromResult<WorkspaceSnapshot?>(null);
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        return dbContext.Workspaces
            .AsNoTracking()
            .Include(x => x.Tenant)
            .Where(x => x.WorkspaceKey == normalizedWorkspaceKey)
            .Select(ToSnapshotExpressionWithTenant())
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<WorkspaceThemeAssignmentSnapshot?> GetThemeAssignmentAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Task.FromResult<WorkspaceThemeAssignmentSnapshot?>(null);
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        return dbContext.Workspaces
            .AsNoTracking()
            .Where(x => x.WorkspaceKey == normalizedWorkspaceKey)
            .Select(x => new WorkspaceThemeAssignmentSnapshot(
                x.WorkspaceKey,
                x.ThemePluginId,
                x.ThemeVersion,
                x.ThemeAssignedBy,
                x.ThemeAssignedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<WorkspaceUpsertResult> UpsertAsync(
        string tenantKey,
        string workspaceKey,
        string displayName,
        string workspaceType,
        bool isActive,
        string? publicBaseUrl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceType);

        var normalizedTenantKey = tenantKey.Trim();
        var normalizedWorkspaceKey = workspaceKey.Trim();
        var normalizedDisplayName = displayName.Trim();
        var normalizedWorkspaceType = workspaceType.Trim();
        if (!WorkspacePublicUrlNormalizer.TryNormalize(publicBaseUrl, out var publicUrl, out _))
        {
            return new WorkspaceUpsertResult(WorkspaceUpsertStatus.InvalidPublicUrl);
        }

        var nowUtc = DateTimeOffset.UtcNow;

        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.TenantKey == normalizedTenantKey, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            return new WorkspaceUpsertResult(WorkspaceUpsertStatus.TenantNotFound);
        }

        var workspace = await dbContext.Workspaces
            .SingleOrDefaultAsync(x => x.WorkspaceKey == normalizedWorkspaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (workspace is null)
        {
            workspace = new WorkspaceEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                WorkspaceKey = normalizedWorkspaceKey,
                DisplayName = normalizedDisplayName,
                WorkspaceType = normalizedWorkspaceType,
                IsActive = isActive,
                PublicBaseUrl = publicUrl.PublicBaseUrl,
                PublicHost = publicUrl.PublicHost,
                PublicPathPrefix = publicUrl.PublicPathPrefix,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };
            dbContext.Workspaces.Add(workspace);
        }
        else
        {
            workspace.TenantId = tenant.Id;
            workspace.DisplayName = normalizedDisplayName;
            workspace.WorkspaceType = normalizedWorkspaceType;
            workspace.IsActive = isActive;
            workspace.PublicBaseUrl = publicUrl.PublicBaseUrl;
            workspace.PublicHost = publicUrl.PublicHost;
            workspace.PublicPathPrefix = publicUrl.PublicPathPrefix;
            workspace.UpdatedAtUtc = nowUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new WorkspaceUpsertResult(WorkspaceUpsertStatus.Ok, ToSnapshot(workspace, tenant));
    }

    public async Task<WorkspaceSnapshot?> ResolveByPublicRouteAsync(
        string requestHost,
        string requestPath,
        string? tenantKey = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Workspaces
            .AsNoTracking()
            .Include(x => x.Tenant)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(tenantKey))
        {
            var normalizedTenantKey = tenantKey.Trim();
            query = query.Where(x => x.Tenant.TenantKey == normalizedTenantKey);
        }

        var candidates = await query
            .Select(ToSnapshotExpressionWithTenant())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return WorkspacePublicRouteMatcher.ResolveBest(candidates, requestHost, requestPath);
    }

    public async Task<bool> RemoveAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return false;
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        var workspace = await dbContext.Workspaces
            .SingleOrDefaultAsync(x => x.WorkspaceKey == normalizedWorkspaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (workspace is null)
        {
            return false;
        }

        dbContext.Workspaces.Remove(workspace);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<WorkspaceThemeAssignmentSnapshot?> UpsertThemeAssignmentAsync(
        string workspaceKey,
        string themePluginId,
        string themeVersion,
        string? assignedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(themePluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(themeVersion);

        var normalizedWorkspaceKey = workspaceKey.Trim();
        var workspace = await dbContext.Workspaces
            .SingleOrDefaultAsync(x => x.WorkspaceKey == normalizedWorkspaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (workspace is null)
        {
            return null;
        }

        workspace.ThemePluginId = themePluginId.Trim();
        workspace.ThemeVersion = themeVersion.Trim();
        workspace.ThemeAssignedBy = string.IsNullOrWhiteSpace(assignedBy) ? null : assignedBy.Trim();
        workspace.ThemeAssignedAtUtc = DateTimeOffset.UtcNow;
        workspace.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new WorkspaceThemeAssignmentSnapshot(
            workspace.WorkspaceKey,
            workspace.ThemePluginId,
            workspace.ThemeVersion,
            workspace.ThemeAssignedBy,
            workspace.ThemeAssignedAtUtc);
    }

    public async Task<bool> ClearThemeAssignmentAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return false;
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        var workspace = await dbContext.Workspaces
            .SingleOrDefaultAsync(x => x.WorkspaceKey == normalizedWorkspaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (workspace is null)
        {
            return false;
        }

        workspace.ThemePluginId = null;
        workspace.ThemeVersion = null;
        workspace.ThemeAssignedBy = null;
        workspace.ThemeAssignedAtUtc = null;
        workspace.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<WorkspaceMemberSnapshot>> ListMembersAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return [];
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        return await dbContext.WorkspaceMemberships
            .AsNoTracking()
            .Where(x => x.Workspace.WorkspaceKey == normalizedWorkspaceKey)
            .OrderBy(x => x.User.ExternalId)
            .Select(x => new WorkspaceMemberSnapshot(
                x.Workspace.WorkspaceKey,
                x.User.ExternalId,
                x.User.Email,
                x.User.DisplayName,
                x.Role,
                x.AssignedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WorkspaceMemberUpsertResult> UpsertMemberAsync(
        string workspaceKey,
        string userExternalId,
        string role,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(userExternalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        var normalizedWorkspaceKey = workspaceKey.Trim();
        var normalizedUserExternalId = userExternalId.Trim();
        var normalizedRole = role.Trim();

        var workspace = await dbContext.Workspaces
            .SingleOrDefaultAsync(x => x.WorkspaceKey == normalizedWorkspaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (workspace is null)
        {
            return new WorkspaceMemberUpsertResult(WorkspaceMemberUpsertStatus.WorkspaceNotFound);
        }

        var user = await dbContext.BackendUsers
            .SingleOrDefaultAsync(x => x.ExternalId == normalizedUserExternalId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return new WorkspaceMemberUpsertResult(WorkspaceMemberUpsertStatus.UserNotFound);
        }

        var membership = await dbContext.WorkspaceMemberships
            .SingleOrDefaultAsync(
                x => x.WorkspaceId == workspace.Id && x.UserId == user.Id,
                cancellationToken)
            .ConfigureAwait(false);

        var nowUtc = DateTimeOffset.UtcNow;
        if (membership is null)
        {
            membership = new WorkspaceMembership
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                UserId = user.Id,
                Role = normalizedRole,
                AssignedAtUtc = nowUtc
            };
            dbContext.WorkspaceMemberships.Add(membership);
        }
        else
        {
            membership.Role = normalizedRole;
            membership.AssignedAtUtc = nowUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new WorkspaceMemberUpsertResult(
            WorkspaceMemberUpsertStatus.Ok,
            new WorkspaceMemberSnapshot(
                workspace.WorkspaceKey,
                user.ExternalId,
                user.Email,
                user.DisplayName,
                membership.Role,
                membership.AssignedAtUtc));
    }

    public async Task<WorkspaceMemberDeleteResult> RemoveMemberAsync(
        string workspaceKey,
        string userExternalId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(userExternalId))
        {
            return new WorkspaceMemberDeleteResult(WorkspaceMemberDeleteStatus.MembershipNotFound);
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        var normalizedUserExternalId = userExternalId.Trim();

        var workspace = await dbContext.Workspaces
            .SingleOrDefaultAsync(x => x.WorkspaceKey == normalizedWorkspaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (workspace is null)
        {
            return new WorkspaceMemberDeleteResult(WorkspaceMemberDeleteStatus.WorkspaceNotFound);
        }

        var user = await dbContext.BackendUsers
            .SingleOrDefaultAsync(x => x.ExternalId == normalizedUserExternalId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return new WorkspaceMemberDeleteResult(WorkspaceMemberDeleteStatus.UserNotFound);
        }

        var membership = await dbContext.WorkspaceMemberships
            .SingleOrDefaultAsync(
                x => x.WorkspaceId == workspace.Id && x.UserId == user.Id,
                cancellationToken)
            .ConfigureAwait(false);
        if (membership is null)
        {
            return new WorkspaceMemberDeleteResult(WorkspaceMemberDeleteStatus.MembershipNotFound);
        }

        dbContext.WorkspaceMemberships.Remove(membership);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new WorkspaceMemberDeleteResult(WorkspaceMemberDeleteStatus.Deleted);
    }

    private static WorkspaceSnapshot ToSnapshot(WorkspaceEntity workspace, Domain.Tenants.Tenant tenant)
    {
        return new WorkspaceSnapshot(
            tenant.TenantKey,
            workspace.WorkspaceKey,
            workspace.DisplayName,
            workspace.WorkspaceType,
            workspace.IsActive,
            tenant.IsActive,
            workspace.PublicBaseUrl,
            workspace.PublicHost,
            workspace.PublicPathPrefix,
            workspace.ThemePluginId,
            workspace.ThemeVersion,
            workspace.ThemeAssignedBy,
            workspace.ThemeAssignedAtUtc,
            workspace.CreatedAtUtc,
            workspace.UpdatedAtUtc);
    }

    private static Expression<Func<WorkspaceEntity, WorkspaceSnapshot>> ToSnapshotExpressionWithTenant()
    {
        return x => new WorkspaceSnapshot(
            x.Tenant.TenantKey,
            x.WorkspaceKey,
            x.DisplayName,
            x.WorkspaceType,
            x.IsActive,
            x.Tenant.IsActive,
            x.PublicBaseUrl,
            x.PublicHost,
            x.PublicPathPrefix,
            x.ThemePluginId,
            x.ThemeVersion,
            x.ThemeAssignedBy,
            x.ThemeAssignedAtUtc,
            x.CreatedAtUtc,
            x.UpdatedAtUtc);
    }
}
