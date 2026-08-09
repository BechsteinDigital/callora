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

        // Dieselben Ablehnungsgründe wie im EF-Store: ein Elternteil, den es nicht gibt, und
        // einer, der einen Zyklus erzeugte. Ein Testdouble, das beides durchließe, ließe jeden
        // Test grün, der sie prüfen soll.
        if (!string.IsNullOrWhiteSpace(input.ParentSurfaceKey))
        {
            var inWorkspace = _surfaces.Values
                .Where(surface => string.Equals(surface.WorkspaceKey, workspaceKey, StringComparison.Ordinal))
                .ToArray();
            if (!inWorkspace.Any(surface =>
                    string.Equals(surface.SurfaceKey, input.ParentSurfaceKey, StringComparison.Ordinal)))
            {
                return Task.FromResult<WorkspaceSurfaceSnapshot?>(null);
            }

            if (WouldCycle(inWorkspace, input.SurfaceKey, input.ParentSurfaceKey))
            {
                return Task.FromResult<WorkspaceSurfaceSnapshot?>(null);
            }
        }
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
            input.Routing,
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
            ParentSurfaceKey = string.IsNullOrWhiteSpace(input.ParentSurfaceKey) ? null : input.ParentSurfaceKey,
            Position = input.Position,
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

    public Task<SurfaceDeleteResult> DeleteAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default)
    {
        // Dieselbe Reihenfolge wie im EF-Store: erst nach Kindern fragen, dann löschen. Ein
        // Testdouble, das den Fall gar nicht kennt, ließe jeden Test grün, der ihn prüfen soll.
        if (!_surfaces.ContainsKey((workspaceKey, surfaceKey)))
        {
            return Task.FromResult(SurfaceDeleteResult.NotFound);
        }

        var hasChildren = _surfaces.Values.Any(surface =>
            string.Equals(surface.WorkspaceKey, workspaceKey, StringComparison.Ordinal) &&
            string.Equals(surface.ParentSurfaceKey, surfaceKey, StringComparison.Ordinal));
        if (hasChildren)
        {
            return Task.FromResult(SurfaceDeleteResult.HasChildren);
        }

        _surfaces.TryRemove((workspaceKey, surfaceKey), out _);
        return Task.FromResult(SurfaceDeleteResult.Deleted);
    }

    /// <summary>Ob dieser Elternteil den Knoten zu seinem eigenen Vorfahren machte.</summary>
    private static bool WouldCycle(
        IReadOnlyList<WorkspaceSurfaceSnapshot> inWorkspace,
        string surfaceKey,
        string parentKey)
    {
        var parentOf = inWorkspace.ToDictionary(
            surface => surface.SurfaceKey,
            surface => surface.ParentSurfaceKey,
            StringComparer.Ordinal);

        var current = parentKey;
        var steps = 0;
        while (current is not null && steps++ <= 32)
        {
            if (string.Equals(current, surfaceKey, StringComparison.Ordinal))
            {
                return true;
            }

            current = parentOf.GetValueOrDefault(current);
        }

        return false;
    }
}
