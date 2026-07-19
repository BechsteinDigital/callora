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
            nowUtc);
        _surfaces[key] = snapshot;
        return Task.FromResult<WorkspaceSurfaceSnapshot?>(snapshot);
    }

    public Task<bool> DeleteAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_surfaces.TryRemove((workspaceKey, surfaceKey), out _));
}
