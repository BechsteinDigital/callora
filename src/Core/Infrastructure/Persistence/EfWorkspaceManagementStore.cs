using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using WorkspaceEntity = Callora.Core.Domain.Workspaces.Workspace;

namespace Callora.Core.Infrastructure.Persistence;

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
        string? defaultSurfaceBaseUrl = null,
        string? publicHost = null,
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
        var normalizedPublicHost = string.IsNullOrWhiteSpace(publicHost)
            ? null
            : publicHost.Trim().ToLowerInvariant();
        if (!WorkspacePublicUrlNormalizer.TryNormalize(defaultSurfaceBaseUrl, out var publicUrl, out _))
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
                PublicHost = normalizedPublicHost,
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
            workspace.PublicHost = normalizedPublicHost;
            workspace.UpdatedAtUtc = nowUtc;
        }

        // Every workspace has a "default" surface — it is the one way into the data
        // until an operator adds more. The route given here configures THAT surface;
        // der Workspace trägt höchstens einen Host, nie einen Pfad (ADR-021).
        var defaultSurface = await dbContext.WorkspaceSurfaces
            .SingleOrDefaultAsync(
                x => x.WorkspaceId == workspace.Id && x.SurfaceKey == "default",
                cancellationToken)
            .ConfigureAwait(false);
        if (defaultSurface is null)
        {
            defaultSurface = new WorkspaceSurface
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                SurfaceKey = "default",
                DisplayName = normalizedDisplayName,
                SurfaceType = "spa",
                Authentication = SurfaceAuthentication.Public,
                CreatedAtUtc = nowUtc,
            };
            dbContext.WorkspaceSurfaces.Add(defaultSurface);
        }

        if (!string.IsNullOrWhiteSpace(defaultSurfaceBaseUrl))
        {
            defaultSurface.PublicBaseUrl = publicUrl.PublicBaseUrl;
            defaultSurface.PublicHost = publicUrl.PublicHost;
            defaultSurface.PublicPathPrefix = publicUrl.PublicPathPrefix;
        }

        defaultSurface.IsActive = isActive;
        defaultSurface.UpdatedAtUtc = nowUtc;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new WorkspaceUpsertResult(WorkspaceUpsertStatus.Ok, ToSnapshot(workspace, tenant));
    }

    public async Task<WorkspaceSnapshot?> ResolveByPublicRouteAsync(
        string requestHost,
        string requestPath,
        string? tenantKey = null,
        CancellationToken cancellationToken = default)
    {
        var (best, _) = await MatchSurfaceByPublicRouteAsync(requestHost, requestPath, tenantKey, cancellationToken)
            .ConfigureAwait(false);
        return best is null ? null : ToSnapshot(best.Workspace, best.Workspace.Tenant);
    }

    public async Task<WorkspaceSurfaceSnapshot?> ResolveSurfaceByPublicRouteAsync(
        string requestHost,
        string requestPath,
        string? tenantKey = null,
        CancellationToken cancellationToken = default)
    {
        var (best, byId) = await MatchSurfaceByPublicRouteAsync(requestHost, requestPath, tenantKey, cancellationToken)
            .ConfigureAwait(false);
        if (best is null)
        {
            return null;
        }

        // Der Renderpfad bekommt die EFFEKTIVEN Werte — was für diesen Knoten gilt, geerbt oder
        // eigen. Die Verwaltung (GetSurfaceAsync, ListSurfacesAsync) bekommt weiterhin die
        // eigenen: Sonst könnte eine Oberfläche einen geerbten Wert nicht von einem gesetzten
        // unterscheiden und machte beim Speichern aus der Vererbung eine Kopie.
        //
        // Die Kette kommt aus der Menge, die der Matcher ohnehin geladen hat. Vorher holte diese
        // Stelle dieselben Zeilen ein zweites Mal aus der Datenbank — pro Anfrage, im heißesten
        // Pfad, den dieses Repository hat. Der Vorfilter im Matcher arbeitet auf WORKSPACE-Ebene,
        // deshalb liegt die vollständige Kette darin: Sie verlässt den Workspace nie.
        return ToEffectiveSurfaceSnapshot(best, EffectiveSurface.From(AncestryOf(best, byId)));
    }

    /// <summary>
    /// Die passendste Fläche für Host und Pfad — und die Menge, in der sie gefunden wurde.
    /// <para>
    /// Die Menge kommt mit zurück, weil der Aufrufer die Vererbungskette daraus baut. Sie ein
    /// zweites Mal zu laden war die teuerste Zeile im öffentlichen Renderpfad.
    /// </para>
    /// </summary>
    private async Task<(WorkspaceSurface? Best, IReadOnlyDictionary<Guid, WorkspaceSurface> ById)>
        MatchSurfaceByPublicRouteAsync(
            string requestHost,
            string requestPath,
            string? tenantKey,
            CancellationToken cancellationToken)
    {
        var normalizedHost = (requestHost ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedPath = PublicRouteMatching.NormalizePath(requestPath);

        // Public routing resolves through surfaces (ADR-014 §5/§14): a workspace's
        // "default" surface mirrors its public route, so today's behaviour is preserved
        // while additional surfaces route to the same workspace.
        var query = dbContext.WorkspaceSurfaces
            .AsNoTracking()
            .Include(x => x.Workspace)
            .ThenInclude(w => w.Tenant)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(tenantKey))
        {
            var normalizedTenantKey = tenantKey.Trim();
            query = query.Where(x => x.Workspace.Tenant.TenantKey == normalizedTenantKey);
        }

        // Vorgefiltert wird über den WORKSPACE, nicht über die einzelne Fläche — und das ist kein
        // Umweg, sondern die Bedingung dafür, dass der Filter überhaupt zulässig ist. Der
        // effektive Host eines Knotens ist der erste gesetzte entlang seiner Kette; ein Filter,
        // der einzelne Flächen wegwirft, kann einem Kind seinen Vorfahren nehmen. AncestryOf
        // bricht dann still ab, das Kind gilt als Wurzel — und eine Fläche ohne eigenen Host
        // matcht als Wildcard JEDEN Host. Aus einer Optimierung wäre so ein Fremdzugriff geworden.
        //
        // Vollständig ist der Filter, weil eine Fläche nur matchen kann, wenn irgendein Knoten
        // ihrer Kette entweder den angefragten Host trägt oder gar keinen (dann greift die
        // Wildcard, ggf. über den Workspace-Host). Beide Fälle sind unten erfasst, und da Ketten
        // den Workspace nie verlassen, kommt mit dem Workspace die ganze Kette mit.
        // ILike statt eines Gleichheitsvergleichs, weil der Vergleich in der Datenbank
        // stattfinden muss und Bestandsdaten aus direkten SQL-Eingriffen groß geschrieben sein
        // können — der Speichervorgang senkt den Host, ältere Zeilen hat er nie angefasst. Dass
        // ILike '%' und '_' als Platzhalter liest, ist hier unschädlich: Der Vorfilter darf zu
        // viel laden, nur nichts verlieren; entschieden wird ohnehin unten am effektiven Wert.
        query = query.Where(x => dbContext.WorkspaceSurfaces
            .Where(candidate =>
                candidate.PublicHost == null ||
                candidate.PublicHost.Trim() == "" ||
                EF.Functions.ILike(candidate.PublicHost, normalizedHost))
            .Select(candidate => candidate.WorkspaceId)
            .Contains(x.WorkspaceId));

        // Ohne feste Reihenfolge entschied bei Gleichstand, in welcher Reihenfolge Postgres die
        // Zeilen zurückgab — zwei Flächen auf derselben Adresse konnten sich von Anfrage zu
        // Anfrage abwechseln. Die Doppelbelegung bleibt eine Fehlkonfiguration; sie soll nur
        // nicht auch noch unvorhersehbar sein.
        var surfaces = await query
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Gematcht wird gegen die EFFEKTIVEN Werte (ADR-019): Ein Kind trägt sein Segment, sein
        // Host kommt von der Wurzel. `/portal/partner` gegen `partner` zu prüfen fände nichts,
        // und ein Kind ohne eigenen Host fiele durch jede Host-Prüfung.
        var byId = surfaces.ToDictionary(surface => surface.Id);

        WorkspaceSurface? best = null;
        var bestScore = int.MinValue;
        foreach (var surface in surfaces)
        {
            if (!surface.Workspace.IsActive || !surface.Workspace.Tenant.IsActive)
            {
                continue;
            }

            var ancestry = AncestryOf(surface, byId);

            // Abgeschaltet gilt nach unten, nicht nur für den Knoten selbst. Vorher nahm das
            // Deaktivieren einer Eltern-Fläche nur sie vom Netz: Ihre Kinder blieben unter ihrer
            // eigenen URL erreichbar — und zwar über Host und Pfad, die sie von genau diesem
            // Elternteil erben. Die Navigation zeigte sie da längst nicht mehr
            // (SurfaceNavigationBuilder schneidet den Teilbaum ab), was den Widerspruch erst
            // recht unsichtbar machte: Der Betreiber sah eine abgeschaltete Gliederung und einen
            // trotzdem ausgelieferten Knoten darunter.
            if (ancestry.Any(node => !node.IsActive))
            {
                continue;
            }

            var effective = EffectiveSurface.From(ancestry);
            if (!PublicRouteMatching.HostMatches(effective.PublicHost, normalizedHost) ||
                !PublicRouteMatching.PathMatches(effective.PublicPathPrefix, normalizedPath))
            {
                continue;
            }

            var score = PublicRouteMatching.Score(effective.PublicHost, effective.PublicPathPrefix);
            if (score <= bestScore)
            {
                continue;
            }

            best = surface;
            bestScore = score;
        }

        return (best, byId);
    }

    /// <summary>
    /// Die Kette eines Knotens aufwärts. Ein Vorfahre, der nicht in der geladenen Menge liegt —
    /// etwa weil ein Mandantenfilter ihn ausschloss —, beendet die Kette, statt sie zu sprengen:
    /// Der Knoten gilt dann als Wurzel, was zu einer Fläche ohne geerbtes Theme führt und nicht
    /// zu einem Fehler beim Besucher.
    /// </summary>
    private static IReadOnlyList<WorkspaceSurface> AncestryOf(
        WorkspaceSurface surface,
        IReadOnlyDictionary<Guid, WorkspaceSurface> byId) =>
        SurfaceTree.AncestryOf(
            surface,
            node => node.Id,
            node => node.ParentSurfaceId,
            byId);

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
            workspace.PublicHost,
            workspace.ThemePluginId,
            workspace.ThemeVersion,
            workspace.ThemeAssignedBy,
            workspace.ThemeAssignedAtUtc,
            workspace.CreatedAtUtc,
            workspace.UpdatedAtUtc);
    }

    private static WorkspaceSurfaceSnapshot ToSurfaceSnapshot(WorkspaceSurface surface)
    {
        return new WorkspaceSurfaceSnapshot(
            surface.Id,
            surface.Workspace.WorkspaceKey,
            surface.SurfaceKey,
            surface.DisplayName,
            surface.SurfaceType,
            surface.PublicBaseUrl,
            surface.PublicHost,
            surface.PublicPathPrefix,
            surface.Authentication,
            surface.Routing,
            surface.Locale,
            surface.TemplatePluginId,
            surface.TemplateVersion,
            surface.ThemePluginId,
            surface.ThemeVersion,
            surface.IsActive,
            surface.CreatedAtUtc,
            surface.UpdatedAtUtc)
        {
            TenantKey = surface.Workspace.Tenant.TenantKey,
            // Carried on the public-route resolution too: the render path decides the
            // surface's identity behaviour from the binding, so it must travel with it.
            IdentityPluginId = surface.IdentityPluginId,
            IdentityVersion = surface.IdentityVersion,
            IdentityAssignedBy = surface.IdentityAssignedBy,
            IdentityAssignedAtUtc = surface.IdentityAssignedAtUtc,
        };
    }

    /// <summary>
    /// Der Knoten mit dem, was für ihn gilt. Identität, Locale und Theme können von einem
    /// Vorfahren kommen; Name, Typ und Zeitstempel gehören immer dem Knoten selbst.
    /// </summary>
    private static WorkspaceSurfaceSnapshot ToEffectiveSurfaceSnapshot(
        WorkspaceSurface surface,
        EffectiveSurface effective)
    {
        return new WorkspaceSurfaceSnapshot(
            surface.Id,
            surface.Workspace.WorkspaceKey,
            surface.SurfaceKey,
            surface.DisplayName,
            surface.SurfaceType,
            surface.PublicBaseUrl,
            effective.PublicHost,
            effective.PublicPathPrefix,
            effective.Authentication,
            // NICHT effective: Routing wird nicht vererbt. Jeder Knoten beantwortet für sich,
            // ob er seine Unterpfade selbst deutet.
            surface.Routing,
            effective.Locale,
            effective.TemplatePluginId,
            effective.TemplateVersion,
            effective.ThemePluginId,
            effective.ThemeVersion,
            surface.IsActive,
            surface.CreatedAtUtc,
            surface.UpdatedAtUtc)
        {
            TenantKey = surface.Workspace.Tenant.TenantKey,
            // Der Identity-Provider kommt von der WURZEL (ADR-019 §4). `IdentityAssignedBy`
            // bleibt beim Knoten leer: Wer eine Zuweisung verantwortet, steht an der Wurzel und
            // wäre hier eine Behauptung über diesen Knoten, die niemand gemacht hat.
            RequiredClaims = effective.RequiredClaims,
            GrantedClaims = effective.GrantedClaims,
            IdentityPluginId = effective.IdentityPluginId,
            IdentityVersion = effective.IdentityVersion,
            IdentityAssignedAtUtc = effective.IdentityAssignedAtUtc,
        };
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
            x.PublicHost,
            x.ThemePluginId,
            x.ThemeVersion,
            x.ThemeAssignedBy,
            x.ThemeAssignedAtUtc,
            x.CreatedAtUtc,
            x.UpdatedAtUtc);
    }
}
