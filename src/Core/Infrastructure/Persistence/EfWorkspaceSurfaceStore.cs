using System.Linq.Expressions;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class EfWorkspaceSurfaceStore(HostPersistenceDbContext dbContext) : IWorkspaceSurfaceStore
{
    public async Task<IReadOnlyList<WorkspaceSurfaceSnapshot>> ListAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return [];
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        return await dbContext.WorkspaceSurfaces
            .AsNoTracking()
            .Where(x => x.Workspace.WorkspaceKey == normalizedWorkspaceKey)
            .OrderBy(x => x.SurfaceKey)
            .Select(ToSnapshot(normalizedWorkspaceKey))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WorkspaceSurfaceSnapshot?> GetAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(surfaceKey))
        {
            return null;
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        var normalizedSurfaceKey = surfaceKey.Trim();
        return await dbContext.WorkspaceSurfaces
            .AsNoTracking()
            .Where(x => x.Workspace.WorkspaceKey == normalizedWorkspaceKey && x.SurfaceKey == normalizedSurfaceKey)
            .Select(ToSnapshot(normalizedWorkspaceKey))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WorkspaceSurfaceSnapshot?> UpsertAsync(
        string workspaceKey,
        WorkspaceSurfaceInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(input.SurfaceKey))
        {
            return null;
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        var workspaceId = await dbContext.Workspaces
            .Where(x => x.WorkspaceKey == normalizedWorkspaceKey)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (workspaceId is null)
        {
            return null;
        }

        var normalizedSurfaceKey = input.SurfaceKey.Trim();
        var nowUtc = DateTimeOffset.UtcNow;
        var surface = await dbContext.WorkspaceSurfaces
            .SingleOrDefaultAsync(
                x => x.WorkspaceId == workspaceId.Value && x.SurfaceKey == normalizedSurfaceKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (surface is null)
        {
            surface = new WorkspaceSurface
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId.Value,
                SurfaceKey = normalizedSurfaceKey,
                CreatedAtUtc = nowUtc,
            };
            dbContext.WorkspaceSurfaces.Add(surface);
        }

        surface.DisplayName = input.DisplayName.Trim();
        surface.SurfaceType = string.IsNullOrWhiteSpace(input.SurfaceType) ? "spa" : input.SurfaceType.Trim();
        surface.PublicBaseUrl = input.PublicBaseUrl;
        surface.PublicHost = input.PublicHost;
        surface.PublicPathPrefix = string.IsNullOrWhiteSpace(input.PublicPathPrefix) ? "/" : input.PublicPathPrefix.Trim();
        surface.AccessMode = input.AccessMode;
        surface.Locale = input.Locale;
        surface.TemplatePluginId = input.TemplatePluginId;
        surface.TemplateVersion = input.TemplateVersion;
        surface.ThemePluginId = input.ThemePluginId;
        surface.ThemeVersion = input.ThemeVersion;
        surface.IsActive = input.IsActive;
        surface.UpdatedAtUtc = nowUtc;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToSnapshotObject(normalizedWorkspaceKey, surface);
    }

    public async Task<bool> DeleteAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(surfaceKey))
        {
            return false;
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        var normalizedSurfaceKey = surfaceKey.Trim();
        var surface = await dbContext.WorkspaceSurfaces
            .SingleOrDefaultAsync(
                x => x.Workspace.WorkspaceKey == normalizedWorkspaceKey && x.SurfaceKey == normalizedSurfaceKey,
                cancellationToken)
            .ConfigureAwait(false);
        if (surface is null)
        {
            return false;
        }

        dbContext.WorkspaceSurfaces.Remove(surface);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static Expression<Func<WorkspaceSurface, WorkspaceSurfaceSnapshot>> ToSnapshot(string workspaceKey) =>
        x => new WorkspaceSurfaceSnapshot(
            x.Id,
            workspaceKey,
            x.SurfaceKey,
            x.DisplayName,
            x.SurfaceType,
            x.PublicBaseUrl,
            x.PublicHost,
            x.PublicPathPrefix,
            x.AccessMode,
            x.Locale,
            x.TemplatePluginId,
            x.TemplateVersion,
            x.ThemePluginId,
            x.ThemeVersion,
            x.IsActive,
            x.CreatedAtUtc,
            x.UpdatedAtUtc);

    private static WorkspaceSurfaceSnapshot ToSnapshotObject(string workspaceKey, WorkspaceSurface x) =>
        new(
            x.Id,
            workspaceKey,
            x.SurfaceKey,
            x.DisplayName,
            x.SurfaceType,
            x.PublicBaseUrl,
            x.PublicHost,
            x.PublicPathPrefix,
            x.AccessMode,
            x.Locale,
            x.TemplatePluginId,
            x.TemplateVersion,
            x.ThemePluginId,
            x.ThemeVersion,
            x.IsActive,
            x.CreatedAtUtc,
            x.UpdatedAtUtc);
}
