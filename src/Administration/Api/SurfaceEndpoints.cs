using Callora.Core.Api;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Security;

namespace Callora.Administration.Api;

/// <summary>
/// Workspace surfaces (ADR-014 §5): the N access/output surfaces of a workspace,
/// managed as a workspace sub-resource under /api/workspaces/{key}/surfaces.
/// </summary>
public static class SurfaceEndpoints
{
    public static IEndpointRouteBuilder MapSurfaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/workspaces/{workspaceKey}/surfaces")
            .WithTags("Surfaces")
            .RequireAuthorization();

        group.MapGet("/", async (
            string workspaceKey,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            IWorkspaceSurfaceStore surfaceStore,
            CancellationToken cancellationToken) =>
        {
            if (!await WorkspaceInScopeAsync(hostOptions, workspaceStore, workspaceKey, cancellationToken).ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var surfaces = await surfaceStore.ListAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            return Results.Ok(surfaces.Select(ToResponse).ToArray());
        }).WithName("Workspaces_Surfaces_List")
            .RequirePermission(BackendPermissionKeys.WorkspaceRead);

        group.MapGet("/{surfaceKey}", async (
            string workspaceKey,
            string surfaceKey,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            IWorkspaceSurfaceStore surfaceStore,
            CancellationToken cancellationToken) =>
        {
            if (!await WorkspaceInScopeAsync(hostOptions, workspaceStore, workspaceKey, cancellationToken).ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var surface = await surfaceStore.GetAsync(workspaceKey, surfaceKey, cancellationToken).ConfigureAwait(false);
            return surface is null ? Results.NotFound() : Results.Ok(ToResponse(surface));
        }).WithName("Workspaces_Surfaces_Get")
            .RequirePermission(BackendPermissionKeys.WorkspaceRead);

        group.MapPut("/{surfaceKey}", async (
            string workspaceKey,
            string surfaceKey,
            UpsertSurfaceApiRequest request,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            IWorkspaceSurfaceStore surfaceStore,
            CancellationToken cancellationToken) =>
        {
            if (!await WorkspaceInScopeAsync(hostOptions, workspaceStore, workspaceKey, cancellationToken).ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            if (!Enum.TryParse<SurfaceAuthentication>(request.Authentication, ignoreCase: true, out var authentication))
            {
                return ApiProblems.BadRequest($"Unknown authentication '{request.Authentication}'. Supported: Public, SurfaceIdentity, Administration.");
            }

            if (!TryParseRouting(request.Routing, out var routing))
            {
                return ApiProblems.BadRequest(
                    $"Unknown routing '{request.Routing}'. Supported: Tree, Application.");
            }

            var input = new WorkspaceSurfaceInput(
                surfaceKey,
                request.DisplayName,
                request.SurfaceType,
                request.PublicBaseUrl,
                request.PublicHost,
                request.PublicPathPrefix,
                authentication,
                request.Locale,
                request.TemplatePluginId,
                request.TemplateVersion,
                request.ThemePluginId,
                request.ThemeVersion,
                request.IsActive)
            {
                ParentSurfaceKey = request.ParentSurfaceKey,
                Position = request.Position,
                RequiredClaims = request.RequiredClaims,
                GrantedClaims = request.GrantedClaims,
                Routing = routing,
            };

            var result = await surfaceStore.UpsertAsync(workspaceKey, input, cancellationToken).ConfigureAwait(false);
            // Null heißt hier zweierlei: kein solcher Workspace, oder ein Elternknoten, den es
            // nicht gibt beziehungsweise der einen Zyklus erzeugte. Die Meldung nennt beides,
            // statt einen Zyklusfehler als fehlenden Workspace auszugeben.
            return result is null
                ? ApiProblems.BadRequest(
                    $"Workspace '{workspaceKey}' not found, or the parent surface does not exist " +
                    "or would create a cycle.")
                : Results.Ok(ToResponse(result));
        }).WithName("Workspaces_Surfaces_Upsert")
            .RequirePermission(BackendPermissionKeys.WorkspaceUpdate);

        group.MapDelete("/{surfaceKey}", async (
            string workspaceKey,
            string surfaceKey,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            IWorkspaceSurfaceStore surfaceStore,
            CancellationToken cancellationToken) =>
        {
            if (!await WorkspaceInScopeAsync(hostOptions, workspaceStore, workspaceKey, cancellationToken).ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var result = await surfaceStore.DeleteAsync(workspaceKey, surfaceKey, cancellationToken).ConfigureAwait(false);
            return result switch
            {
                SurfaceDeleteResult.Deleted => Results.NoContent(),
                // 409 und nicht 404: Der Knoten ist da, er lässt sich nur nicht so löschen.
                SurfaceDeleteResult.HasChildren => ApiProblems.Conflict(
                    $"Surface '{surfaceKey}' has child surfaces. Move or delete them first."),
                _ => Results.NotFound(),
            };
        }).WithName("Workspaces_Surfaces_Delete")
            .RequirePermission(BackendPermissionKeys.WorkspaceUpdate);

        return endpoints;
    }

    /// <summary>
    /// Weggelassen heißt <see cref="SurfaceRouting.Tree"/>, ein unbekannter Wert heißt Fehler.
    /// </summary>
    /// <remarks>
    /// Der Unterschied ist wichtig: Ein Tippfehler still als Baum zu behandeln, machte aus einer
    /// gemeinten Anwendung eine, die jeden ihrer Instanzpfade mit 404 beantwortet — und niemand
    /// erführe, dass der Wert nie angekommen ist.
    /// </remarks>
    private static bool TryParseRouting(string? value, out SurfaceRouting routing)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            routing = SurfaceRouting.Tree;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out routing) && Enum.IsDefined(routing);
    }

    private static async Task<bool> WorkspaceInScopeAsync(
        BackendHostOptions hostOptions,
        IWorkspaceManagementStore workspaceStore,
        string workspaceKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hostOptions.DefaultTenantKey))
        {
            return false;
        }

        var workspace = await workspaceStore.GetAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        return workspace is not null &&
               string.Equals(workspace.TenantKey, hostOptions.DefaultTenantKey, StringComparison.OrdinalIgnoreCase);
    }

    private static SurfaceApiResponse ToResponse(WorkspaceSurfaceSnapshot surface) => new(
        surface.Id,
        surface.WorkspaceKey,
        surface.SurfaceKey,
        surface.DisplayName,
        surface.SurfaceType,
        surface.PublicBaseUrl,
        surface.PublicHost,
        surface.PublicPathPrefix,
        surface.Authentication.ToString(),
        surface.Routing.ToString(),
        surface.Locale,
        surface.TemplatePluginId,
        surface.TemplateVersion,
        surface.ThemePluginId,
        surface.ThemeVersion,
        surface.IsActive,
        surface.CreatedAtUtc,
        surface.UpdatedAtUtc)
    {
        ParentSurfaceKey = surface.ParentSurfaceKey,
        Position = surface.Position,
        RequiredClaims = surface.RequiredClaims,
        GrantedClaims = surface.GrantedClaims,
    };
}
