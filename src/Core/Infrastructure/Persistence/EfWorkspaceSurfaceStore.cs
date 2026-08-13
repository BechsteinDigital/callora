using System.Linq.Expressions;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class EfWorkspaceSurfaceStore(
    HostPersistenceDbContext dbContext,
    ISurfaceRouteTable routeTable) : IWorkspaceSurfaceStore
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
        surface.Authentication = input.Authentication;
        surface.Routing = input.Routing;
        surface.Locale = input.Locale;
        surface.TemplatePluginId = input.TemplatePluginId;
        surface.TemplateVersion = input.TemplateVersion;
        surface.ThemePluginId = input.ThemePluginId;
        surface.ThemeVersion = input.ThemeVersion;
        surface.IsActive = input.IsActive;
        surface.Position = input.Position;
        surface.RequiredClaims = input.RequiredClaims;
        surface.GrantedClaims = input.GrantedClaims;
        surface.UpdatedAtUtc = nowUtc;

        // Der Elternteil zuletzt, weil er als Einziger scheitern kann. Ein abgelehnter
        // Elternteil darf die Surface nicht halb geschrieben zurücklassen.
        if (!await TrySetParentAsync(surface, workspaceId.Value, input.ParentSurfaceKey, cancellationToken)
                .ConfigureAwait(false))
        {
            return null;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        routeTable.Invalidate();

        return ToSnapshotObject(
            normalizedWorkspaceKey,
            surface,
            string.IsNullOrWhiteSpace(input.ParentSurfaceKey) ? null : input.ParentSurfaceKey.Trim());
    }

    /// <summary>
    /// Setzt den Elternknoten, oder lehnt ab.
    /// <para>
    /// Abgelehnt wird ein Elternteil aus einem anderen Workspace, einer, den es nicht gibt, und
    /// jeder, der einen Zyklus erzeugte. Die Zyklusprüfung gehört hierher und nicht in den
    /// Renderpfad: Ein Zyklus, der erst beim Auflösen aufliefe, wäre eine Endlosschleife für
    /// jeden Besucher — nicht nur für den, der ihn angelegt hat.
    /// </para>
    /// </summary>
    private async Task<bool> TrySetParentAsync(
        WorkspaceSurface surface,
        Guid workspaceId,
        string? parentSurfaceKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parentSurfaceKey))
        {
            surface.ParentSurfaceId = null;
            return true;
        }

        var normalizedParentKey = parentSurfaceKey.Trim();
        var siblings = await dbContext.WorkspaceSurfaces
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspaceId)
            .Select(x => new { x.Id, x.SurfaceKey, x.ParentSurfaceId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var parent = siblings.SingleOrDefault(x =>
            string.Equals(x.SurfaceKey, normalizedParentKey, StringComparison.Ordinal));
        if (parent is null)
        {
            return false;
        }

        var parentById = siblings.ToDictionary(x => x.Id, x => x.ParentSurfaceId);
        if (SurfaceTree.WouldCreateCycle(surface.Id, parent.Id, parentById))
        {
            return false;
        }

        surface.ParentSurfaceId = parent.Id;
        return true;
    }

    public async Task<WorkspaceSurfaceSnapshot?> AssignIdentityProviderAsync(
        string workspaceKey,
        string surfaceKey,
        string? pluginId,
        string? version,
        string? assignedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(surfaceKey))
        {
            return null;
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
            return null;
        }

        var normalizedPluginId = string.IsNullOrWhiteSpace(pluginId) ? null : pluginId.Trim();
        var nowUtc = DateTimeOffset.UtcNow;

        surface.IdentityPluginId = normalizedPluginId;
        surface.IdentityVersion = normalizedPluginId is null || string.IsNullOrWhiteSpace(version)
            ? null
            : version.Trim();
        surface.IdentityAssignedBy = normalizedPluginId is null || string.IsNullOrWhiteSpace(assignedBy)
            ? null
            : assignedBy.Trim();
        // Stamped on clearing too: it is the instant every previously issued session
        // stops being trusted, and that boundary must exist even without a provider.
        surface.IdentityAssignedAtUtc = nowUtc;
        surface.UpdatedAtUtc = nowUtc;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        routeTable.Invalidate();

        return ToSnapshotObject(normalizedWorkspaceKey, surface);
    }

    public async Task<SurfaceDeleteResult> DeleteAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(surfaceKey))
        {
            return SurfaceDeleteResult.NotFound;
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
            return SurfaceDeleteResult.NotFound;
        }

        // Vor dem Löschen fragen, nicht die Datenbank fragen lassen: Der Fremdschlüssel steht
        // auf Restrict, also käme der Versuch als Serverfehler beim Operator an — und ein
        // Serverfehler sagt nicht, dass da drei Unterseiten hängen.
        var hasChildren = await dbContext.WorkspaceSurfaces
            .AnyAsync(x => x.ParentSurfaceId == surface.Id, cancellationToken)
            .ConfigureAwait(false);
        if (hasChildren)
        {
            return SurfaceDeleteResult.HasChildren;
        }

        dbContext.WorkspaceSurfaces.Remove(surface);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        routeTable.Invalidate();
        return SurfaceDeleteResult.Deleted;
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
            x.Authentication,
            x.Routing,
            x.Locale,
            x.TemplatePluginId,
            x.TemplateVersion,
            x.ThemePluginId,
            x.ThemeVersion,
            x.IsActive,
            x.CreatedAtUtc,
            x.UpdatedAtUtc)
        {
            ParentSurfaceKey = x.Parent!.SurfaceKey,
            Position = x.Position,
            RequiredClaims = x.RequiredClaims,
            GrantedClaims = x.GrantedClaims,
            IdentityPluginId = x.IdentityPluginId,
            IdentityVersion = x.IdentityVersion,
            IdentityAssignedBy = x.IdentityAssignedBy,
            IdentityAssignedAtUtc = x.IdentityAssignedAtUtc,
        };

    /// <summary>
    /// Nach einem Schreibvorgang: Der Elternteil ist als Id gesetzt, aber nicht geladen — sein
    /// Schlüssel kommt deshalb als Parameter. Ihn aus <c>x.Parent</c> zu lesen ergäbe still
    /// null, und die Antwort behauptete eine Wurzel, wo gerade ein Kind entstanden ist.
    /// </summary>
    private static WorkspaceSurfaceSnapshot ToSnapshotObject(
        string workspaceKey,
        WorkspaceSurface x,
        string? parentSurfaceKey = null) =>
        new(
            x.Id,
            workspaceKey,
            x.SurfaceKey,
            x.DisplayName,
            x.SurfaceType,
            x.PublicBaseUrl,
            x.PublicHost,
            x.PublicPathPrefix,
            x.Authentication,
            x.Routing,
            x.Locale,
            x.TemplatePluginId,
            x.TemplateVersion,
            x.ThemePluginId,
            x.ThemeVersion,
            x.IsActive,
            x.CreatedAtUtc,
            x.UpdatedAtUtc)
        {
            ParentSurfaceKey = parentSurfaceKey,
            Position = x.Position,
            RequiredClaims = x.RequiredClaims,
            GrantedClaims = x.GrantedClaims,
            IdentityPluginId = x.IdentityPluginId,
            IdentityVersion = x.IdentityVersion,
            IdentityAssignedBy = x.IdentityAssignedBy,
            IdentityAssignedAtUtc = x.IdentityAssignedAtUtc,
        };
}
