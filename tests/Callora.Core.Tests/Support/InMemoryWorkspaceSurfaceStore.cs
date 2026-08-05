using System.Collections.Concurrent;
using Callora.Core.Application.Workspaces;

namespace Callora.Core.Tests.Support;

/// <summary>In-memory <see cref="IWorkspaceSurfaceStore"/> for endpoint tests.</summary>
public sealed class InMemoryWorkspaceSurfaceStore : IWorkspaceSurfaceStore
{
    private readonly ConcurrentDictionary<(string Workspace, string Surface), WorkspaceSurfaceSnapshot> _surfaces =
        new();

    public Task<IReadOnlyList<WorkspaceSurfaceSnapshot>> ListAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkspaceSurfaceSnapshot>>(
            _surfaces.Values
                .Where(x => x.WorkspaceKey == workspaceKey)
                .OrderBy(x => x.SurfaceKey, StringComparer.Ordinal)
                .ToList());

    public Task<WorkspaceSurfaceSnapshot?> GetAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_surfaces.GetValueOrDefault((workspaceKey, surfaceKey)));

    public Task<WorkspaceSurfaceSnapshot?> UpsertAsync(
        string workspaceKey,
        WorkspaceSurfaceInput input,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var key = (workspaceKey, input.SurfaceKey);
        var existing = _surfaces.GetValueOrDefault(key);
        var snapshot = new WorkspaceSurfaceSnapshot(
            existing?.Id ?? Guid.NewGuid(),
            workspaceKey,
            input.SurfaceKey,
            input.DisplayName,
            input.SurfaceType,
            input.PublicBaseUrl,
            input.PublicHost,
            input.PublicPathPrefix,
            input.AccessMode,
            input.Locale,
            input.TemplatePluginId,
            input.TemplateVersion,
            input.ThemePluginId,
            input.ThemeVersion,
            input.IsActive,
            existing?.CreatedAtUtc ?? nowUtc,
            nowUtc)
        {
            // A surface edit carries neither tenant nor identity binding, so both
            // survive it here exactly as they do in the EF store.
            TenantKey = existing?.TenantKey ?? string.Empty,
            IdentityPluginId = existing?.IdentityPluginId,
            IdentityVersion = existing?.IdentityVersion,
            IdentityAssignedBy = existing?.IdentityAssignedBy,
            IdentityAssignedAtUtc = existing?.IdentityAssignedAtUtc,
        };
        _surfaces[key] = snapshot;
        return Task.FromResult<WorkspaceSurfaceSnapshot?>(snapshot);
    }

    public Task<WorkspaceSurfaceSnapshot?> AssignIdentityProviderAsync(
        string workspaceKey,
        string surfaceKey,
        string? pluginId,
        string? version,
        string? assignedBy,
        CancellationToken cancellationToken = default)
    {
        var key = (workspaceKey, surfaceKey);
        if (_surfaces.GetValueOrDefault(key) is not { } existing)
        {
            return Task.FromResult<WorkspaceSurfaceSnapshot?>(null);
        }

        var normalizedPluginId = string.IsNullOrWhiteSpace(pluginId) ? null : pluginId.Trim();
        var snapshot = existing with
        {
            IdentityPluginId = normalizedPluginId,
            IdentityVersion = normalizedPluginId is null ? null : version,
            IdentityAssignedBy = normalizedPluginId is null ? null : assignedBy,
            IdentityAssignedAtUtc = DateTimeOffset.UtcNow,
        };
        _surfaces[key] = snapshot;
        return Task.FromResult<WorkspaceSurfaceSnapshot?>(snapshot);
    }

    /// <summary>Seeds a surface snapshot verbatim, bypassing the upsert projection.</summary>
    public void Seed(WorkspaceSurfaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _surfaces[(snapshot.WorkspaceKey, snapshot.SurfaceKey)] = snapshot;
    }

    public Task<bool> DeleteAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_surfaces.TryRemove((workspaceKey, surfaceKey), out _));
}
