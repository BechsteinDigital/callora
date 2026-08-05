using Callora.Core.Api;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Workspaces;
using Callora.Core.Infrastructure.Security;

namespace Callora.Administration.Api;

/// <summary>
/// Operator control over who authenticates a surface's visitors (ADR-017 §5). The
/// candidate list is filtered on the <c>surface.identity</c> capability rather than
/// offering every installed plugin, and a change ends the sessions the previous
/// provider vouched for.
/// </summary>
public static class SurfaceIdentityEndpoints
{
    public static void MapSurfaceIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/api/surfaces/workspaces/{workspaceKey}/surfaces/{surfaceKey}/identity")
            .WithTags("Surfaces")
            .RequireAuthorization();

        group.MapGet("", async (
            string workspaceKey,
            string surfaceKey,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            SurfaceIdentityAssignmentService service,
            CancellationToken cancellationToken) =>
        {
            if (!await IsInConfiguredTenantAsync(workspaceKey, hostOptions, workspaceStore, cancellationToken)
                    .ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var result = await service.GetAsync(workspaceKey, surfaceKey, cancellationToken).ConfigureAwait(false);
            return result.Status == SurfaceIdentityAssignmentStatus.Ok && result.Assignment is not null
                ? Results.Ok(ToResponse(result.Assignment))
                : ToProblem(result.Status, result.Message);
        }).WithName("Surfaces_Identity_Get")
            .RequirePermission(BackendPermissionKeys.ExtensionRead);

        group.MapGet("/candidates", async (
            string workspaceKey,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            SurfaceIdentityAssignmentService service,
            CancellationToken cancellationToken) =>
        {
            if (!await IsInConfiguredTenantAsync(workspaceKey, hostOptions, workspaceStore, cancellationToken)
                    .ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var candidates = await service.ListCandidatesAsync(workspaceKey, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(candidates.Select(ToResponse).ToArray());
        }).WithName("Surfaces_Identity_Candidates")
            .RequirePermission(BackendPermissionKeys.ExtensionRead);

        group.MapPut("", async (
            string workspaceKey,
            string surfaceKey,
            SurfaceIdentityAssignmentUpsertApiRequest request,
            HttpContext httpContext,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            SurfaceIdentityAssignmentService service,
            CancellationToken cancellationToken) =>
        {
            if (!await IsInConfiguredTenantAsync(workspaceKey, hostOptions, workspaceStore, cancellationToken)
                    .ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            if (string.IsNullOrWhiteSpace(request?.IdentityPluginId))
            {
                return ApiProblems.BadRequest("identityPluginId is required.");
            }

            var result = await service
                .AssignAsync(workspaceKey, surfaceKey, request.IdentityPluginId, Actor(httpContext), cancellationToken)
                .ConfigureAwait(false);
            return result.Status == SurfaceIdentityAssignmentStatus.Ok && result.Assignment is not null
                ? Results.Ok(ToResponse(result.Assignment))
                : ToProblem(result.Status, result.Message);
        }).WithName("Surfaces_Identity_Assign")
            .RequirePermission(BackendPermissionKeys.ExtensionUpdate);

        group.MapDelete("", async (
            string workspaceKey,
            string surfaceKey,
            HttpContext httpContext,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            SurfaceIdentityAssignmentService service,
            CancellationToken cancellationToken) =>
        {
            if (!await IsInConfiguredTenantAsync(workspaceKey, hostOptions, workspaceStore, cancellationToken)
                    .ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var result = await service
                .ClearAsync(workspaceKey, surfaceKey, Actor(httpContext), cancellationToken)
                .ConfigureAwait(false);
            return result.Status == SurfaceIdentityAssignmentStatus.Ok && result.Assignment is not null
                ? Results.Ok(ToResponse(result.Assignment))
                : ToProblem(result.Status, result.Message);
        }).WithName("Surfaces_Identity_Clear")
            .RequirePermission(BackendPermissionKeys.ExtensionUpdate);
    }

    private static string? Actor(HttpContext httpContext) =>
        httpContext.User.Identity?.Name;

    private static SurfaceIdentityAssignmentApiResponse ToResponse(SurfaceIdentityAssignment assignment) =>
        new(
            assignment.WorkspaceKey,
            assignment.SurfaceKey,
            assignment.PluginId,
            assignment.Version,
            assignment.AssignedBy,
            assignment.AssignedAtUtc,
            assignment.IsAvailable);

    private static SurfaceIdentityProviderCandidateApiResponse ToResponse(
        SurfaceIdentityProviderCandidate candidate) =>
        new(candidate.PluginId, candidate.DisplayName, candidate.Version, candidate.IsAvailable);

    private static IResult ToProblem(SurfaceIdentityAssignmentStatus status, string? message) => status switch
    {
        SurfaceIdentityAssignmentStatus.WorkspaceNotFound => ApiProblems.NotFound(message ?? "Workspace not found."),
        SurfaceIdentityAssignmentStatus.SurfaceNotFound => ApiProblems.NotFound(message ?? "Surface not found."),
        SurfaceIdentityAssignmentStatus.PluginNotFound => ApiProblems.NotFound(message ?? "Plugin not found."),
        // A plugin without the capability is a bad request, not a missing one: the
        // operator named something real that simply cannot do the job.
        SurfaceIdentityAssignmentStatus.CapabilityMissing =>
            ApiProblems.BadRequest(message ?? "Plugin cannot provide surface identity."),
        _ => ApiProblems.BadRequest(message ?? "Assignment failed."),
    };

    private static async Task<bool> IsInConfiguredTenantAsync(
        string workspaceKey,
        BackendHostOptions hostOptions,
        IWorkspaceManagementStore workspaceStore,
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
}
