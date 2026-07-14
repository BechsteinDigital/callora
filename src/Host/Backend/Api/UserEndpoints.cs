using Callora.Host.Backend.Application.Abstractions.Security;
using Callora.Host.Backend.Domain.Security;
using Callora.Host.Backend.Infrastructure.Security;

namespace Callora.Host.Backend.Api;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapGet("/", async (
            HttpContext httpContext,
            IBackendUserStore userStore,
            CancellationToken cancellationToken) =>
        {
            var (isOperator, workspaceKey) = ResolveScope(httpContext);

            // Operators (super admins) see every user; a workspace-scoped
            // caller only sees the users of its own workspace (H1).
            var users = isOperator
                ? await userStore.ListAsync(cancellationToken).ConfigureAwait(false)
                : string.IsNullOrWhiteSpace(workspaceKey)
                    ? []
                    : await userStore.ListByWorkspaceAsync(workspaceKey, cancellationToken).ConfigureAwait(false);

            return Results.Ok(users.Select(ToResponse).ToArray());
        }).WithName("Users_List")
            .RequirePermission(BackendPermissionKeys.UserRead);

        group.MapGet("/{userId}", async (
            string userId,
            HttpContext httpContext,
            IBackendUserStore userStore,
            CancellationToken cancellationToken) =>
        {
            if (!await CallerMayAccessAsync(httpContext, userStore, userId, cancellationToken).ConfigureAwait(false))
            {
                return Results.NotFound();
            }

            var user = await userStore.GetByExternalIdAsync(userId, cancellationToken).ConfigureAwait(false);
            return user is null ? Results.NotFound() : Results.Ok(ToResponse(user));
        }).WithName("Users_Get")
            .RequirePermission(BackendPermissionKeys.UserRead);

        group.MapPost("/", async (
            CreateBackendUserApiRequest request,
            HttpContext httpContext,
            IBackendUserStore userStore,
            CancellationToken cancellationToken) =>
        {
            // Creating a global user record is a platform operation; workspace
            // admins manage membership of existing users, not global identities.
            if (!WorkspaceScopeEvaluator.IsOperator(httpContext.User))
            {
                return Results.Forbid();
            }

            try
            {
                var user = await userStore.UpsertCredentialsAsync(
                        request.ExternalId,
                        request.Email,
                        request.DisplayName,
                        request.Password,
                        cancellationToken)
                    .ConfigureAwait(false);
                return Results.Created($"/api/users/{user.ExternalId}", ToResponse(user));
            }
            catch (InvalidOperationException ex)
            {
                return ApiProblems.BadRequest(ex.Message);
            }
        }).WithName("Users_Create")
            .RequirePermission(BackendPermissionKeys.UserCreate);

        group.MapPut("/{userId}", async (
            string userId,
            UpdateBackendUserApiRequest request,
            HttpContext httpContext,
            IBackendUserStore userStore,
            CancellationToken cancellationToken) =>
        {
            if (!await CallerMayAccessAsync(httpContext, userStore, userId, cancellationToken).ConfigureAwait(false))
            {
                return Results.NotFound();
            }

            try
            {
                var user = await userStore.UpsertCredentialsAsync(
                        userId,
                        request.Email,
                        request.DisplayName,
                        request.Password,
                        cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(ToResponse(user));
            }
            catch (InvalidOperationException ex)
            {
                return ApiProblems.BadRequest(ex.Message);
            }
        }).WithName("Users_Update")
            .RequirePermission(BackendPermissionKeys.UserUpdate);

        group.MapDelete("/{userId}", async (
            string userId,
            HttpContext httpContext,
            IBackendUserStore userStore,
            IUserDataSubjectService dataSubjectService,
            CancellationToken cancellationToken) =>
        {
            if (!await CallerMayAccessAsync(httpContext, userStore, userId, cancellationToken).ConfigureAwait(false))
            {
                return Results.NotFound();
            }

            // Art. 17: Löschung inklusive Anonymisierung des Audit-Trails
            // (PLAT-243).
            var removed = await dataSubjectService.EraseAsync(userId, cancellationToken).ConfigureAwait(false);
            return removed ? Results.NoContent() : Results.NotFound();
        }).WithName("Users_Delete")
            .RequirePermission(BackendPermissionKeys.UserDelete);

        group.MapGet("/{userId}/data-export", async (
            string userId,
            HttpContext httpContext,
            IBackendUserStore userStore,
            IUserDataSubjectService dataSubjectService,
            CancellationToken cancellationToken) =>
        {
            if (!await CallerMayAccessAsync(httpContext, userStore, userId, cancellationToken).ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"User '{userId}' not found.");
            }

            var export = await dataSubjectService.ExportAsync(userId, cancellationToken).ConfigureAwait(false);
            return export is null
                ? ApiProblems.NotFound($"User '{userId}' not found.")
                : Results.Ok(export);
        }).WithName("Users_DataExport")
            .Produces<UserDataExport>()
            .RequirePermission(BackendPermissionKeys.UserRead);
    }

    /// <summary>
    /// Reads the caller's operator status and bound workspace from its claims.
    /// Operators act platform-wide; everyone else is confined to a workspace.
    /// </summary>
    private static (bool IsOperator, string? WorkspaceKey) ResolveScope(HttpContext httpContext)
    {
        var isOperator = WorkspaceScopeEvaluator.IsOperator(httpContext.User);
        var workspaceKey = httpContext.User.FindFirst(BackendClaimTypes.WorkspaceKey)?.Value;
        return (isOperator, workspaceKey);
    }

    /// <summary>
    /// True when the caller may act on <paramref name="userId"/>: operators
    /// always, workspace-scoped callers only for members of their own
    /// workspace. Returns false (surfaced as 404) for cross-workspace access,
    /// so foreign users are not even revealed to exist (H1).
    /// </summary>
    private static async Task<bool> CallerMayAccessAsync(
        HttpContext httpContext,
        IBackendUserStore userStore,
        string userId,
        CancellationToken cancellationToken)
    {
        var (isOperator, workspaceKey) = ResolveScope(httpContext);
        if (isOperator)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(workspaceKey) &&
               await userStore.IsWorkspaceMemberAsync(userId, workspaceKey, cancellationToken).ConfigureAwait(false);
    }

    private static BackendUserApiResponse ToResponse(BackendUser user)
    {
        return new BackendUserApiResponse(
            ExternalId: user.ExternalId,
            Email: user.Email,
            DisplayName: user.DisplayName,
            HasPassword: !string.IsNullOrWhiteSpace(user.PasswordHash),
            PasswordHashAlgorithm: user.PasswordHashAlgorithm,
            CreatedAtUtc: user.CreatedAtUtc,
            UpdatedAtUtc: user.UpdatedAtUtc);
    }
}
