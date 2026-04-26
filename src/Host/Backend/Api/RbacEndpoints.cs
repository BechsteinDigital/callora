using Callora.Host.Backend.Application.Abstractions.Security;
using Callora.Host.Backend.Infrastructure.Security;
using Callora.Modules.Abstractions.Application.Plugins;
using Microsoft.AspNetCore.Mvc;
using VoipHost.PluginContracts.Application.Plugins;

namespace Callora.Host.Backend.Api;

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
            var staticPermissions = typeof(BackendPermissionKeys)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(field => field.FieldType == typeof(string))
                .Select(field => field.GetValue(null) as string)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToArray();

            var pluginPermissions = pluginCatalog
                .GetExports<IHostAdminApiExtensionContributor>()
                .SelectMany(contributor => contributor.PermissionKeys)
                .Where(BackendPermissionKeyValidator.IsValid)
                .ToArray();

            var permissions = staticPermissions
                .Concat(pluginPermissions)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .Select(value =>
                {
                    var parts = value.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
                    var function = parts.Length > 0 ? parts[0] : value;
                    var action = parts.Length > 1 ? parts[1] : string.Empty;
                    return new RbacPermissionApiResponse(value, function, action);
                })
                .ToArray();

            return Results.Ok(permissions);
        })
            .WithName("Rbac_Permissions_List")
            .RequirePermission(BackendPermissionKeys.RoleRead);

        group.MapPut("/roles/{role}", async (string role, RbacRoleUpsertApiRequest request, [FromServices] IBackendRbacStore store, CancellationToken cancellationToken) =>
        {
            var permissions = request.Functions
                .SelectMany(x => x.Actions.Select(action => $"{x.Function.Trim().ToLowerInvariant()}.{action.Trim().ToLowerInvariant()}"))
                .ToArray();

            await store.UpsertRoleAsync(role, permissions, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new RbacRoleApiResponse(role, permissions));
        })
            .WithName("Rbac_Roles_Upsert")
            .RequirePermission(BackendPermissionKeys.RoleUpdate);

        group.MapDelete("/roles/{role}", async (string role, [FromServices] IBackendRbacStore store, CancellationToken cancellationToken) =>
        {
            var removed = await store.RemoveRoleAsync(role, cancellationToken).ConfigureAwait(false);
            return removed ? Results.NoContent() : Results.NotFound();
        })
            .WithName("Rbac_Roles_Delete")
            .RequirePermission(BackendPermissionKeys.RoleUpdate);

        group.MapGet("/users", async ([FromServices] IBackendRbacStore store, CancellationToken cancellationToken) =>
        {
            var users = (await store.GetUserRolesAsync(cancellationToken).ConfigureAwait(false))
                .Select(x => new RbacUserApiResponse(x.Key, x.Value))
                .OrderBy(x => x.UserId, StringComparer.Ordinal)
                .ToArray();

            return Results.Ok(users);
        })
            .WithName("Rbac_Users_List")
            .RequirePermission(BackendPermissionKeys.UserRead);

        group.MapPut("/users/{userId}", async (string userId, RbacUserUpsertApiRequest request, [FromServices] IBackendRbacStore store, CancellationToken cancellationToken) =>
        {
            await store.UpsertUserRoleAsync(userId, request.Role, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new RbacUserApiResponse(userId, request.Role));
        })
            .WithName("Rbac_Users_Upsert")
            .RequirePermission(BackendPermissionKeys.UserUpdate);

        group.MapDelete("/users/{userId}", async (string userId, [FromServices] IBackendRbacStore store, CancellationToken cancellationToken) =>
        {
            var removed = await store.RemoveUserRoleAsync(userId, cancellationToken).ConfigureAwait(false);
            return removed ? Results.NoContent() : Results.NotFound();
        })
            .WithName("Rbac_Users_Delete")
            .RequirePermission(BackendPermissionKeys.UserUpdate);
    }
}
