using Callora.Host.Backend.Api;
using Callora.Host.Backend.Application.Workspaces;
using Callora.Host.Backend.Infrastructure.Persistence;
using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Infrastructure.Security;

namespace Callora.Host.Workspace.Api;

public static class WorkspaceEndpoints
{
    public static void MapWorkspaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/workspaces")
            .WithTags("Workspaces")
            .RequireAuthorization();

        group.MapGet("/", async (
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hostOptions.DefaultTenantKey))
            {
                return ApiProblems.BadRequest("Workspace host default tenant key is not configured.");
            }

            var workspaces = await workspaceStore.ListAsync(hostOptions.DefaultTenantKey, cancellationToken).ConfigureAwait(false);
            return Results.Ok(workspaces.Select(ToResponse).ToArray());
        }).WithName("Workspaces_List")
            .RequirePermission(BackendPermissionKeys.WorkspaceRead);

        group.MapGet("/{workspaceKey}", async (
            string workspaceKey,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hostOptions.DefaultTenantKey))
            {
                return ApiProblems.BadRequest("Workspace host default tenant key is not configured.");
            }

            var workspace = await workspaceStore.GetAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            if (workspace is not null &&
                !string.Equals(workspace.TenantKey, hostOptions.DefaultTenantKey, StringComparison.OrdinalIgnoreCase))
            {
                workspace = null;
            }

            return workspace is null ? Results.NotFound() : Results.Ok(ToResponse(workspace));
        }).WithName("Workspaces_Get")
            .RequirePermission(BackendPermissionKeys.WorkspaceRead);

        group.MapPut("/{workspaceKey}", async (
            string workspaceKey,
            UpsertWorkspaceApiRequest request,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            CancellationToken cancellationToken) =>
        {
            var tenantKey = hostOptions.DefaultTenantKey;

            if (string.IsNullOrWhiteSpace(tenantKey))
            {
                return ApiProblems.BadRequest("No tenant key provided and no workspace host default tenant key configured.");
            }

            var workspace = await workspaceStore
                .UpsertAsync(
                    tenantKey,
                    workspaceKey,
                    request.DisplayName,
                    request.WorkspaceType,
                    request.IsActive,
                    request.PublicBaseUrl,
                    cancellationToken)
                .ConfigureAwait(false);

            return workspace.Status switch
            {
                WorkspaceUpsertStatus.Ok when workspace.Workspace is not null => Results.Ok(ToResponse(workspace.Workspace)),
                WorkspaceUpsertStatus.TenantNotFound => ApiProblems.NotFound($"Tenant '{tenantKey}' not found."),
                WorkspaceUpsertStatus.InvalidPublicUrl => ApiProblems.BadRequest("Workspace public URL is invalid."),
                _ => Results.BadRequest()
            };
        }).WithName("Workspaces_Upsert")
            .RequirePermission(BackendPermissionKeys.WorkspaceUpdate);

        group.MapDelete("/{workspaceKey}", async (
            string workspaceKey,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            IWorkspaceDataPurgeService purgeService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hostOptions.DefaultTenantKey))
            {
                return ApiProblems.BadRequest("Workspace host default tenant key is not configured.");
            }

            var workspace = await workspaceStore.GetAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            if (workspace is null ||
                !string.Equals(workspace.TenantKey, hostOptions.DefaultTenantKey, StringComparison.OrdinalIgnoreCase))
            {
                return Results.NotFound();
            }

            // Kaskadierende Löschung: Workspace + alle workspace-gebundenen
            // Daten in einer Transaktion (DSGVO, PLAT-242).
            var removed = await purgeService.PurgeAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            return removed ? Results.NoContent() : Results.NotFound();
        }).WithName("Workspaces_Delete")
            .RequirePermission(BackendPermissionKeys.WorkspaceDelete);

        group.MapGet("/{workspaceKey}/members", async (
            string workspaceKey,
            int? limit,
            string? cursor,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hostOptions.DefaultTenantKey))
            {
                return ApiProblems.BadRequest("Workspace host default tenant key is not configured.");
            }

            var workspace = await workspaceStore.GetAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            if (workspace is null ||
                !string.Equals(workspace.TenantKey, hostOptions.DefaultTenantKey, StringComparison.OrdinalIgnoreCase))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var members = await workspaceStore.ListMembersAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            var ordered = members
                .Select(ToResponse)
                .OrderBy(static x => x.UserId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return Results.Ok(ListPagination.Page(
                ordered, limit, cursor, static x => x.UserId));
        }).WithName("Workspaces_Members_List")
            .Produces<PagedApiResponse<WorkspaceMemberApiResponse>>()
            .RequirePermission(BackendPermissionKeys.WorkspaceRead);

        group.MapPut("/{workspaceKey}/members/{userId}", async (
            string workspaceKey,
            string userId,
            UpsertWorkspaceMemberApiRequest request,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hostOptions.DefaultTenantKey))
            {
                return ApiProblems.BadRequest("Workspace host default tenant key is not configured.");
            }

            var workspace = await workspaceStore.GetAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            if (workspace is null ||
                !string.Equals(workspace.TenantKey, hostOptions.DefaultTenantKey, StringComparison.OrdinalIgnoreCase))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var result = await workspaceStore
                .UpsertMemberAsync(workspaceKey, userId, request.Role, cancellationToken)
                .ConfigureAwait(false);

            return result.Status switch
            {
                WorkspaceMemberUpsertStatus.Ok when result.Member is not null => Results.Ok(ToResponse(result.Member)),
                WorkspaceMemberUpsertStatus.WorkspaceNotFound => ApiProblems.NotFound($"Workspace '{workspaceKey}' not found."),
                WorkspaceMemberUpsertStatus.UserNotFound => ApiProblems.NotFound($"User '{userId}' not found."),
                _ => Results.BadRequest()
            };
        }).WithName("Workspaces_Members_Upsert")
            .RequirePermission(BackendPermissionKeys.WorkspaceUpdate);

        group.MapDelete("/{workspaceKey}/members/{userId}", async (
            string workspaceKey,
            string userId,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hostOptions.DefaultTenantKey))
            {
                return ApiProblems.BadRequest("Workspace host default tenant key is not configured.");
            }

            var workspace = await workspaceStore.GetAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            if (workspace is null ||
                !string.Equals(workspace.TenantKey, hostOptions.DefaultTenantKey, StringComparison.OrdinalIgnoreCase))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var result = await workspaceStore.RemoveMemberAsync(workspaceKey, userId, cancellationToken).ConfigureAwait(false);
            return result.Status switch
            {
                WorkspaceMemberDeleteStatus.Deleted => Results.NoContent(),
                WorkspaceMemberDeleteStatus.WorkspaceNotFound => ApiProblems.NotFound($"Workspace '{workspaceKey}' not found."),
                WorkspaceMemberDeleteStatus.UserNotFound => ApiProblems.NotFound($"User '{userId}' not found."),
                WorkspaceMemberDeleteStatus.MembershipNotFound => ApiProblems.NotFound($"Membership '{workspaceKey}/{userId}' not found."),
                _ => Results.BadRequest()
            };
        }).WithName("Workspaces_Members_Delete")
            .RequirePermission(BackendPermissionKeys.WorkspaceUpdate);
    }

    private static WorkspaceApiResponse ToResponse(WorkspaceSnapshot workspace)
    {
        return new WorkspaceApiResponse(
            workspace.TenantKey,
            workspace.WorkspaceKey,
            workspace.DisplayName,
            workspace.WorkspaceType,
            workspace.IsActive,
            workspace.TenantIsActive,
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

    private static WorkspaceMemberApiResponse ToResponse(WorkspaceMemberSnapshot member)
    {
        return new WorkspaceMemberApiResponse(
            member.WorkspaceKey,
            member.UserId,
            member.Email,
            member.DisplayName,
            member.Role,
            member.AssignedAtUtc);
    }
}
