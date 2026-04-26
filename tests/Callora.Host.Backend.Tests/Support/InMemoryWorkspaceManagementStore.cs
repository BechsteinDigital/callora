using System.Collections.Concurrent;
using Callora.Host.Backend.Application.Abstractions.Workspaces;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class InMemoryWorkspaceManagementStore : IWorkspaceManagementStore
{
    private readonly ConcurrentDictionary<string, WorkspaceSnapshot> _workspaces = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WorkspaceMemberSnapshot>> _members = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _knownUsers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _tenants = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<WorkspaceSnapshot>> ListAsync(
        string? tenantKey = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var source = _workspaces.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(tenantKey))
        {
            source = source.Where(x => string.Equals(x.TenantKey, tenantKey.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<WorkspaceSnapshot>>(
            source.OrderBy(x => x.WorkspaceKey, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public Task<WorkspaceSnapshot?> GetAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Task.FromResult<WorkspaceSnapshot?>(null);
        }

        _workspaces.TryGetValue(workspaceKey.Trim(), out var workspace);
        return Task.FromResult(workspace);
    }

    public Task<WorkspaceThemeAssignmentSnapshot?> GetThemeAssignmentAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(workspaceKey) || !_workspaces.TryGetValue(workspaceKey.Trim(), out var workspace))
        {
            return Task.FromResult<WorkspaceThemeAssignmentSnapshot?>(null);
        }

        return Task.FromResult<WorkspaceThemeAssignmentSnapshot?>(
            new WorkspaceThemeAssignmentSnapshot(
                workspace.WorkspaceKey,
                workspace.ThemePluginId,
                workspace.ThemeVersion,
                workspace.ThemeAssignedBy,
                workspace.ThemeAssignedAtUtc));
    }

    public Task<WorkspaceUpsertResult> UpsertAsync(
        string tenantKey,
        string workspaceKey,
        string displayName,
        string workspaceType,
        bool isActive,
        string? publicBaseUrl = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceType);

        var normalizedTenantKey = tenantKey.Trim();
        if (!_tenants.TryGetValue(normalizedTenantKey, out var tenantIsActive))
        {
            return Task.FromResult(new WorkspaceUpsertResult(WorkspaceUpsertStatus.TenantNotFound));
        }

        if (!WorkspacePublicUrlNormalizer.TryNormalize(publicBaseUrl, out var publicUrl, out _))
        {
            return Task.FromResult(new WorkspaceUpsertResult(WorkspaceUpsertStatus.InvalidPublicUrl));
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        var nowUtc = DateTimeOffset.UtcNow;
        var existed = _workspaces.TryGetValue(normalizedWorkspaceKey, out var existing);
        var workspace = new WorkspaceSnapshot(
            normalizedTenantKey,
            normalizedWorkspaceKey,
            displayName.Trim(),
            workspaceType.Trim(),
            isActive,
            tenantIsActive,
            publicUrl.PublicBaseUrl,
            publicUrl.PublicHost,
            publicUrl.PublicPathPrefix,
            existed ? existing!.ThemePluginId : null,
            existed ? existing!.ThemeVersion : null,
            existed ? existing!.ThemeAssignedBy : null,
            existed ? existing!.ThemeAssignedAtUtc : null,
            existed ? existing!.CreatedAtUtc : nowUtc,
            nowUtc);
        _workspaces[normalizedWorkspaceKey] = workspace;
        _members.TryAdd(normalizedWorkspaceKey, new ConcurrentDictionary<string, WorkspaceMemberSnapshot>(StringComparer.OrdinalIgnoreCase));
        return Task.FromResult(new WorkspaceUpsertResult(WorkspaceUpsertStatus.Ok, workspace));
    }

    public Task<WorkspaceSnapshot?> ResolveByPublicRouteAsync(
        string requestHost,
        string requestPath,
        string? tenantKey = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = _workspaces.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(tenantKey))
        {
            var normalizedTenantKey = tenantKey.Trim();
            candidates = candidates.Where(x => string.Equals(x.TenantKey, normalizedTenantKey, StringComparison.OrdinalIgnoreCase));
        }

        var resolved = WorkspacePublicRouteMatcher.ResolveBest(candidates, requestHost, requestPath);
        return Task.FromResult(resolved);
    }

    public Task<bool> RemoveAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Task.FromResult(false);
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        _members.TryRemove(normalizedWorkspaceKey, out _);
        return Task.FromResult(_workspaces.TryRemove(normalizedWorkspaceKey, out _));
    }

    public Task<WorkspaceThemeAssignmentSnapshot?> UpsertThemeAssignmentAsync(
        string workspaceKey,
        string themePluginId,
        string themeVersion,
        string? assignedBy,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(themePluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(themeVersion);

        if (!_workspaces.TryGetValue(workspaceKey.Trim(), out var workspace))
        {
            return Task.FromResult<WorkspaceThemeAssignmentSnapshot?>(null);
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var updated = workspace with
        {
            ThemePluginId = themePluginId.Trim(),
            ThemeVersion = themeVersion.Trim(),
            ThemeAssignedBy = string.IsNullOrWhiteSpace(assignedBy) ? null : assignedBy.Trim(),
            ThemeAssignedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
        _workspaces[updated.WorkspaceKey] = updated;

        return Task.FromResult<WorkspaceThemeAssignmentSnapshot?>(
            new WorkspaceThemeAssignmentSnapshot(
                updated.WorkspaceKey,
                updated.ThemePluginId,
                updated.ThemeVersion,
                updated.ThemeAssignedBy,
                updated.ThemeAssignedAtUtc));
    }

    public Task<bool> ClearThemeAssignmentAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(workspaceKey) || !_workspaces.TryGetValue(workspaceKey.Trim(), out var workspace))
        {
            return Task.FromResult(false);
        }

        _workspaces[workspace.WorkspaceKey] = workspace with
        {
            ThemePluginId = null,
            ThemeVersion = null,
            ThemeAssignedBy = null,
            ThemeAssignedAtUtc = null,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<WorkspaceMemberSnapshot>> ListMembersAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Task.FromResult<IReadOnlyList<WorkspaceMemberSnapshot>>([]);
        }

        if (!_members.TryGetValue(workspaceKey.Trim(), out var members))
        {
            return Task.FromResult<IReadOnlyList<WorkspaceMemberSnapshot>>([]);
        }

        return Task.FromResult<IReadOnlyList<WorkspaceMemberSnapshot>>(
            members.Values.OrderBy(x => x.UserId, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public Task<WorkspaceMemberUpsertResult> UpsertMemberAsync(
        string workspaceKey,
        string userExternalId,
        string role,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(workspaceKey) || !_workspaces.TryGetValue(workspaceKey.Trim(), out var workspace))
        {
            return Task.FromResult(new WorkspaceMemberUpsertResult(WorkspaceMemberUpsertStatus.WorkspaceNotFound));
        }

        if (string.IsNullOrWhiteSpace(userExternalId))
        {
            return Task.FromResult(new WorkspaceMemberUpsertResult(WorkspaceMemberUpsertStatus.UserNotFound));
        }

        var normalizedUserId = userExternalId.Trim();
        if (!_knownUsers.ContainsKey(normalizedUserId))
        {
            return Task.FromResult(new WorkspaceMemberUpsertResult(WorkspaceMemberUpsertStatus.UserNotFound));
        }

        var normalizedWorkspaceKey = workspace.WorkspaceKey;
        var member = new WorkspaceMemberSnapshot(
            normalizedWorkspaceKey,
            normalizedUserId,
            null,
            null,
            role.Trim(),
            DateTimeOffset.UtcNow);
        var workspaceMembers = _members.GetOrAdd(
            normalizedWorkspaceKey,
            _ => new ConcurrentDictionary<string, WorkspaceMemberSnapshot>(StringComparer.OrdinalIgnoreCase));
        workspaceMembers[normalizedUserId] = member;
        return Task.FromResult(new WorkspaceMemberUpsertResult(WorkspaceMemberUpsertStatus.Ok, member));
    }

    public Task<WorkspaceMemberDeleteResult> RemoveMemberAsync(
        string workspaceKey,
        string userExternalId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(workspaceKey) || !_workspaces.ContainsKey(workspaceKey.Trim()))
        {
            return Task.FromResult(new WorkspaceMemberDeleteResult(WorkspaceMemberDeleteStatus.WorkspaceNotFound));
        }

        if (string.IsNullOrWhiteSpace(userExternalId))
        {
            return Task.FromResult(new WorkspaceMemberDeleteResult(WorkspaceMemberDeleteStatus.UserNotFound));
        }

        if (!_members.TryGetValue(workspaceKey.Trim(), out var members))
        {
            return Task.FromResult(new WorkspaceMemberDeleteResult(WorkspaceMemberDeleteStatus.MembershipNotFound));
        }

        return Task.FromResult(members.TryRemove(userExternalId.Trim(), out _)
            ? new WorkspaceMemberDeleteResult(WorkspaceMemberDeleteStatus.Deleted)
            : new WorkspaceMemberDeleteResult(WorkspaceMemberDeleteStatus.MembershipNotFound));
    }

    public void AddKnownUser(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            _knownUsers.TryAdd(userId.Trim(), 1);
        }
    }

    public void AddTenant(string tenantKey, bool isActive = true)
    {
        if (!string.IsNullOrWhiteSpace(tenantKey))
        {
            _tenants[tenantKey.Trim()] = isActive;
        }
    }

    public void SetTenantActive(string tenantKey, bool isActive)
    {
        if (!string.IsNullOrWhiteSpace(tenantKey))
        {
            _tenants[tenantKey.Trim()] = isActive;
            foreach (var workspace in _workspaces)
            {
                if (string.Equals(workspace.Value.TenantKey, tenantKey.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    _workspaces[workspace.Key] = workspace.Value with { TenantIsActive = isActive, UpdatedAtUtc = DateTimeOffset.UtcNow };
                }
            }
        }
    }
}
