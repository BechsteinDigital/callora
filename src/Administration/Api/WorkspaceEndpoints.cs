using Callora.Core.Api;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Application.Workspaces;
using Callora.Core.Application.Workspaces.Events;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Persistence;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Callora.Administration.Api;

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
            IBusinessEventBus businessEventBus,
            ILoggerFactory loggerFactory,
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
                    request.DefaultSurfaceBaseUrl,
                    request.PublicHost,
                    cancellationToken)
                .ConfigureAwait(false);

            if (workspace.Status == WorkspaceUpsertStatus.Ok && workspace.Workspace is not null)
            {
                await businessEventBus.PublishSafelyAsync(
                    WorkspaceBusinessEvent.ForUpsert(workspace.Workspace),
                    loggerFactory,
                    cancellationToken).ConfigureAwait(false);
                return Results.Ok(ToResponse(workspace.Workspace));
            }

            return workspace.Status switch
            {
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
            IBusinessEventBus businessEventBus,
            ILoggerFactory loggerFactory,
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
            if (!removed)
            {
                return Results.NotFound();
            }

            await businessEventBus.PublishSafelyAsync(
                WorkspaceBusinessEvent.ForDeletion(workspace),
                loggerFactory,
                cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        }).WithName("Workspaces_Delete")
            .RequirePermission(BackendPermissionKeys.WorkspaceDelete);

        // Workspace-membership administration (#102). A workspace-bound caller
        // reaches only its own workspace; operators reach every workspace. This
        // is the surface a workspace administrator uses instead of the global
        // /api/users write endpoints.
        group.MapGet("/{workspaceKey}/members", async (
            string workspaceKey,
            int? limit,
            string? cursor,
            HttpContext httpContext,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            CancellationToken cancellationToken) =>
        {
            if (!WorkspaceScopeEvaluator.HasWorkspaceAccess(httpContext.User, workspaceKey))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

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
            .RequireAnyPermission(
                BackendPermissionKeys.MembershipRead,
                BackendPermissionKeys.WorkspaceRead);

        group.MapPut("/{workspaceKey}/members/{userId}", async (
            string workspaceKey,
            string userId,
            UpsertWorkspaceMemberApiRequest request,
            HttpContext httpContext,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            IBusinessEventBus businessEventBus,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            if (!WorkspaceScopeEvaluator.HasWorkspaceAccess(httpContext.User, workspaceKey))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            if (string.IsNullOrWhiteSpace(hostOptions.DefaultTenantKey))
            {
                return ApiProblems.BadRequest("Workspace host default tenant key is not configured.");
            }

            // Die Mitgliedsrolle darf keine Plattform-Rolle benennen. Sie wird beim Anmelden
            // zum Rollen-Claim, und auf `superadmin` antwortet die Berechtigungsprüfung
            // bedingungslos mit Ja — ein Workspace-Admin hätte sich damit selbst zum Operator
            // über alle Mandanten gemacht. Die Anmeldung weist solche Zeilen zusätzlich ab
            // (AdminLoginResolver); hier entstehen sie erst gar nicht.
            if (ReservedMembershipRoles.IsReserved(request.Role, hostOptions))
            {
                return ApiProblems.BadRequest(
                    $"Role '{request.Role}' is reserved for platform operators and cannot be a workspace membership role.");
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

            if (result.Status == WorkspaceMemberUpsertStatus.Ok && result.Member is not null)
            {
                await businessEventBus.PublishSafelyAsync(
                    WorkspaceMemberBusinessEvent.Assigned(result.Member),
                    loggerFactory,
                    cancellationToken).ConfigureAwait(false);
                return Results.Ok(ToResponse(result.Member));
            }

            return result.Status switch
            {
                WorkspaceMemberUpsertStatus.WorkspaceNotFound => ApiProblems.NotFound($"Workspace '{workspaceKey}' not found."),
                WorkspaceMemberUpsertStatus.UserNotFound => ApiProblems.NotFound($"User '{userId}' not found."),
                _ => Results.BadRequest()
            };
        }).WithName("Workspaces_Members_Upsert")
            .RequireAnyPermission(
                BackendPermissionKeys.MembershipUpdate,
                BackendPermissionKeys.WorkspaceUpdate);

        // Rollen je Mitgliedschaft. Die Mitgliedsrolle darüber sagt „Administrator oder nicht"; hier
        // steht alles, was feiner ist — „darf die Telefonanlage benutzen, aber nichts ändern" ist genau
        // das, wofür es vorher keinen Ort gab.
        //
        // Dieselben Rollen wie global, kein zweites Rollensystem: Was eine Rolle enthält, steht in
        // backend_rbac_roles, und was davon in DIESEM Workspace gilt, entscheidet die Anmeldung.
        group.MapGet("/{workspaceKey}/members/{userId}/roles", async (
            string workspaceKey,
            string userId,
            HttpContext httpContext,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            // Ausdrücklich aus den Diensten: Minimal-APIs raten sonst „Body" für jeden Typ, den der
            // Container beim Aufbau der Route nicht kennt — und in einem Testhost, der nur die Hälfte
            // registriert, wird aus einer GET-Route eine, die einen Rumpf verlangt.
            [FromServices] IWorkspaceMembershipRoleStore membershipRoles,
            CancellationToken cancellationToken) =>
        {
            if (await ResolveWorkspaceAsync(workspaceKey, httpContext, hostOptions, workspaceStore, cancellationToken)
                .ConfigureAwait(false) is { } problem)
            {
                return problem;
            }

            var roles = await membershipRoles
                .ListRolesAsync(workspaceKey, userId, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new WorkspaceMemberRolesApiResponse(userId, roles));
        }).WithName("Workspaces_Members_Roles_List")
            .Produces<WorkspaceMemberRolesApiResponse>()
            .RequireAnyPermission(
                BackendPermissionKeys.MembershipRead,
                BackendPermissionKeys.WorkspaceRead);

        group.MapPut("/{workspaceKey}/members/{userId}/roles", async (
            string workspaceKey,
            string userId,
            SetWorkspaceMemberRolesApiRequest request,
            HttpContext httpContext,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            [FromServices] IWorkspaceMembershipRoleStore membershipRoles,
            [FromServices] IBackendUserStore userStore,
            CancellationToken cancellationToken) =>
        {
            if (await ResolveWorkspaceAsync(workspaceKey, httpContext, hostOptions, workspaceStore, cancellationToken)
                .ConfigureAwait(false) is { } problem)
            {
                return problem;
            }

            // Eine Plattform-Rolle hier zuzuweisen bewirkt nichts — ihre Schlüssel werden bei der
            // Anmeldung ohnehin auf das gefiltert, was im Workspace gelten darf. Trotzdem abgewiesen:
            // Eine Zuweisung, die in der Oberfläche steht und nichts tut, ist eine Falle, und der
            // Betreiber liest sie als „hat Operator-Rechte".
            var reserved = (request.Roles ?? [])
                .FirstOrDefault(role => ReservedMembershipRoles.IsReserved(role, hostOptions));
            if (reserved is not null)
            {
                return ApiProblems.BadRequest(
                    $"Role '{reserved}' is reserved for platform operators and cannot be assigned in a workspace.");
            }

            var stored = await membershipRoles
                .ReplaceRolesAsync(workspaceKey, userId, request.Roles ?? [], cancellationToken)
                .ConfigureAwait(false);

            if (stored is null)
            {
                return ApiProblems.NotFound($"User '{userId}' is not a member of workspace '{workspaceKey}'.");
            }

            // Berechtigungen stehen im Token, nicht in der Datenbank: Ohne den Widerruf behielte
            // jemand, dem eine Rolle gerade entzogen wurde, sie bis zum Ablauf seiner Sitzung.
            await userStore.RevokeSessionsAsync(userId, cancellationToken).ConfigureAwait(false);

            return Results.Ok(new WorkspaceMemberRolesApiResponse(userId, stored));
        }).WithName("Workspaces_Members_Roles_Set")
            .Produces<WorkspaceMemberRolesApiResponse>()
            .RequireAnyPermission(
                BackendPermissionKeys.MembershipUpdate,
                BackendPermissionKeys.WorkspaceUpdate);

        group.MapDelete("/{workspaceKey}/members/{userId}", async (
            string workspaceKey,
            string userId,
            HttpContext httpContext,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            IBusinessEventBus businessEventBus,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            if (!WorkspaceScopeEvaluator.HasWorkspaceAccess(httpContext.User, workspaceKey))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

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
            if (result.Status == WorkspaceMemberDeleteStatus.Deleted)
            {
                await businessEventBus.PublishSafelyAsync(
                    WorkspaceMemberBusinessEvent.Removed(workspaceKey, userId),
                    loggerFactory,
                    cancellationToken).ConfigureAwait(false);
                return Results.NoContent();
            }

            return result.Status switch
            {
                WorkspaceMemberDeleteStatus.WorkspaceNotFound => ApiProblems.NotFound($"Workspace '{workspaceKey}' not found."),
                WorkspaceMemberDeleteStatus.UserNotFound => ApiProblems.NotFound($"User '{userId}' not found."),
                WorkspaceMemberDeleteStatus.MembershipNotFound => ApiProblems.NotFound($"Membership '{workspaceKey}/{userId}' not found."),
                _ => Results.BadRequest()
            };
        }).WithName("Workspaces_Members_Delete")
            .RequireAnyPermission(
                BackendPermissionKeys.MembershipDelete,
                BackendPermissionKeys.WorkspaceUpdate);

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
            workspace.PublicHost,
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

    /// <summary>
    /// Die drei Prüfungen, die jede Mitglieder-Route zuerst macht, oder das Problem, das sie beendet.
    /// </summary>
    /// <remarks>
    /// Zusammengefasst, weil sie zusammengehören und weil eine vergessene davon nicht auffällt: Die
    /// Sichtbarkeitsprüfung fehlt, und die Route antwortet für jeden Workspace.
    /// </remarks>
    private static async Task<IResult?> ResolveWorkspaceAsync(
        string workspaceKey,
        HttpContext httpContext,
        BackendHostOptions hostOptions,
        IWorkspaceManagementStore workspaceStore,
        CancellationToken cancellationToken)
    {
        if (!WorkspaceScopeEvaluator.HasWorkspaceAccess(httpContext.User, workspaceKey))
        {
            return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
        }

        if (string.IsNullOrWhiteSpace(hostOptions.DefaultTenantKey))
        {
            return ApiProblems.BadRequest("Workspace host default tenant key is not configured.");
        }

        var workspace = await workspaceStore.GetAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        return workspace is null ||
            !string.Equals(workspace.TenantKey, hostOptions.DefaultTenantKey, StringComparison.OrdinalIgnoreCase)
            ? ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.")
            : null;
    }
}
