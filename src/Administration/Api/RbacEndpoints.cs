using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace Callora.Administration.Api;

public static class RbacEndpoints
{
    public static void MapRbacEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/security/rbac")
            .WithTags("Security")
            .RequireAuthorization();

        group.MapGet("/roles", async ([FromServices] IBackendRbacStore store, CancellationToken cancellationToken) =>
        {
            var roles = (await store.GetRolePermissionsAsync(cancellationToken).ConfigureAwait(false))
                .Select(x => new RbacRoleApiResponse(x.Key, x.Value.OrderBy(y => y, StringComparer.Ordinal).ToArray()))
                .OrderBy(x => x.Role, StringComparer.Ordinal)
                .ToArray();

            return Results.Ok(roles);
        })
            .WithName("Rbac_Roles_List")
            .RequirePermission(BackendPermissionKeys.RoleRead);

        group.MapGet("/permissions", ([FromServices] ICalloraPluginCatalog pluginCatalog) =>
        {
            var permissions = BackendPermissionInventory.All(pluginCatalog)
                // Dieselbe Zerlegung wie die Gültigkeitsprüfung: Aktion ist das LETZTE Segment.
                // Am ersten Punkt zu teilen machte aus `communication.accounts.read` die Funktion
                // „communication" mit der Aktion „accounts.read" — und aus zwei getrennten
                // Berechtigungen in der Oberfläche eine Gruppe, die keine ist.
                .Select(value =>
                {
                    BackendPermissionKey.TryParse(value, out var key);
                    return new RbacPermissionApiResponse(value, key.Function, key.Action);
                })
                .ToArray();

            return Results.Ok(permissions);
        })
            .WithName("Rbac_Permissions_List")
            .RequirePermission(BackendPermissionKeys.RoleRead);

        group.MapPut("/roles/{role}", async (
            string role,
            RbacRoleUpsertApiRequest request,
            [FromServices] IBackendRbacStore store,
            [FromServices] IBackendUserStore userStore,
            CancellationToken cancellationToken) =>
        {
            var permissions = request.Functions
                .SelectMany(x => x.Actions.Select(action => $"{x.Function.Trim().ToLowerInvariant()}.{action.Trim().ToLowerInvariant()}"))
                .ToArray();

            await store.UpsertRoleAsync(role, permissions, cancellationToken).ConfigureAwait(false);
            await RevokeSessionsOfRoleMembersAsync(role, store, userStore, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new RbacRoleApiResponse(role, permissions));
        })
            .WithName("Rbac_Roles_Upsert")
            .RequirePermission(BackendPermissionKeys.RoleUpdate);

        group.MapDelete("/roles/{role}", async (
            string role,
            [FromServices] IBackendRbacStore store,
            [FromServices] IBackendUserStore userStore,
            CancellationToken cancellationToken) =>
        {
            // Collect the members first — after removal the assignments are gone.
            var members = await ResolveRoleMembersAsync(role, store, cancellationToken).ConfigureAwait(false);
            var removed = await store.RemoveRoleAsync(role, cancellationToken).ConfigureAwait(false);
            if (removed)
            {
                await RevokeSessionsAsync(members, userStore, cancellationToken).ConfigureAwait(false);
            }

            return removed ? Results.NoContent() : Results.NotFound();
        })
            .WithName("Rbac_Roles_Delete")
            .RequirePermission(BackendPermissionKeys.RoleUpdate);

        // These routes manage GLOBAL RBAC role assignments on the unscoped
        // BackendRbacUserRoles table (no WorkspaceKey → not covered by the
        // workspace query filter/write-backstop). They are platform RBAC
        // administration and MUST be gated on role.* — consistent with /roles
        // and /permissions above. They must NOT use user.* : that key lives in
        // the workspace-admin floor (WorkspaceRolePermissions, for the
        // workspace-scoped /api/users endpoints) and would otherwise let any
        // workspace admin read all platform role assignments and escalate
        // themselves to super admin.
        group.MapGet("/users", async ([FromServices] IBackendRbacStore store, CancellationToken cancellationToken) =>
        {
            var users = (await store.GetUserRolesAsync(cancellationToken).ConfigureAwait(false))
                .Select(x => new RbacUserApiResponse(x.Key, x.Value))
                .OrderBy(x => x.UserId, StringComparer.Ordinal)
                .ToArray();

            return Results.Ok(users);
        })
            .WithName("Rbac_Users_List")
            .RequirePermission(BackendPermissionKeys.RoleRead);

        group.MapPut("/users/{userId}", async (
            string userId,
            RbacUserUpsertApiRequest request,
            [FromServices] IBackendRbacStore store,
            [FromServices] IBackendUserStore userStore,
            CancellationToken cancellationToken) =>
        {
            await store.UpsertUserRoleAsync(userId, request.Role, cancellationToken).ConfigureAwait(false);
            // The user's authority just changed; sessions issued under the old role
            // must not survive it (#105).
            await userStore.RevokeSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new RbacUserApiResponse(userId, request.Role));
        })
            .WithName("Rbac_Users_Upsert")
            .RequirePermission(BackendPermissionKeys.RoleUpdate);

        group.MapDelete("/users/{userId}", async (
            string userId,
            [FromServices] IBackendRbacStore store,
            [FromServices] IBackendUserStore userStore,
            CancellationToken cancellationToken) =>
        {
            var removed = await store.RemoveUserRoleAsync(userId, cancellationToken).ConfigureAwait(false);
            if (removed)
            {
                await userStore.RevokeSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
            }

            return removed ? Results.NoContent() : Results.NotFound();
        })
            .WithName("Rbac_Users_Delete")
            .RequirePermission(BackendPermissionKeys.RoleUpdate);
    }

    /// <summary>
    /// External ids of the accounts currently assigned <paramref name="role"/>.
    /// </summary>
    private static async Task<IReadOnlyList<string>> ResolveRoleMembersAsync(
        string role,
        IBackendRbacStore store,
        CancellationToken cancellationToken)
    {
        var assignments = await store.GetUserRolesAsync(cancellationToken).ConfigureAwait(false);
        return assignments
            .Where(x => string.Equals(x.Value, role, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Key)
            .ToArray();
    }

    /// <summary>
    /// Changing a role's grants changes what its members may do. Their live sessions
    /// carry the old permission claims, so they are revoked (#105) — fail-closed:
    /// members re-authenticate and receive the new grants.
    /// </summary>
    private static async Task RevokeSessionsOfRoleMembersAsync(
        string role,
        IBackendRbacStore store,
        IBackendUserStore userStore,
        CancellationToken cancellationToken)
    {
        var members = await ResolveRoleMembersAsync(role, store, cancellationToken).ConfigureAwait(false);
        await RevokeSessionsAsync(members, userStore, cancellationToken).ConfigureAwait(false);
    }

    private static async Task RevokeSessionsAsync(
        IReadOnlyList<string> userIds,
        IBackendUserStore userStore,
        CancellationToken cancellationToken)
    {
        foreach (var userId in userIds)
        {
            await userStore.RevokeSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
        }
    }
}
