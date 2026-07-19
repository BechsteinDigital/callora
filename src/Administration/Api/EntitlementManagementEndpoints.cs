using Callora.Core.Api;
using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;

namespace Callora.Administration.Api;

/// <summary>
/// Operator-facing entitlement administration: list the recorded entitlement
/// decisions and directly grant/revoke a plugin for a scope. This is the direct
/// operator control used in single-seller mode — distinct from the inbound
/// marketplace sync (<see cref="EntitlementSyncEndpoints"/>), which is async and
/// event-sourced for an external marketplace.
/// </summary>
public static class EntitlementManagementEndpoints
{
    public static IEndpointRouteBuilder MapEntitlementManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/entitlements")
            .WithTags("Entitlements")
            .RequireAuthorization();

        group.MapGet("/", async (
            IPluginEntitlementStore store,
            CancellationToken cancellationToken) =>
        {
            var entitlements = await store.ListAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(entitlements.Select(ToResponse).ToArray());
        }).WithName("Entitlements_List")
            .RequirePermission(BackendPermissionKeys.PluginRead);

        group.MapPut("/", async (
            SetEntitlementApiRequest request,
            IPluginEntitlementStore store,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.PluginId))
            {
                return ApiProblems.BadRequest("pluginId is required.");
            }

            await store.SetEntitledAsync(
                request.PluginId,
                request.IsEntitled,
                request.WorkspaceKey,
                request.TenantKey,
                cancellationToken).ConfigureAwait(false);

            return Results.NoContent();
        }).WithName("Entitlements_Set")
            .RequirePermission(BackendPermissionKeys.PluginExecute);

        return app;
    }

    private static EntitlementApiResponse ToResponse(PluginEntitlementSnapshot snapshot) => new(
        snapshot.PluginId,
        snapshot.WorkspaceKey,
        snapshot.TenantKey,
        snapshot.IsEntitled,
        snapshot.Source,
        snapshot.CreatedAtUtc,
        snapshot.UpdatedAtUtc);
}
