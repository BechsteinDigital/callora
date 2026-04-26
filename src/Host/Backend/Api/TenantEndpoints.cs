using Callora.Host.Backend.Application.Abstractions.Tenants;
using Callora.Host.Backend.Infrastructure.Security;

namespace Callora.Host.Backend.Api;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tenants")
            .WithTags("Tenants")
            .RequireAuthorization();

        group.MapGet("/", async (
            ITenantManagementStore tenantStore,
            CancellationToken cancellationToken) =>
        {
            var tenants = await tenantStore.ListAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(tenants.Select(ToResponse).ToArray());
        }).WithName("Tenants_List")
            .RequirePermission(BackendPermissionKeys.TenantRead);

        group.MapGet("/{tenantKey}", async (
            string tenantKey,
            ITenantManagementStore tenantStore,
            CancellationToken cancellationToken) =>
        {
            var tenant = await tenantStore.GetAsync(tenantKey, cancellationToken).ConfigureAwait(false);
            return tenant is null ? Results.NotFound() : Results.Ok(ToResponse(tenant));
        }).WithName("Tenants_Get")
            .RequirePermission(BackendPermissionKeys.TenantRead);

        group.MapPost("/", async (
            CreateTenantApiRequest request,
            ITenantManagementStore tenantStore,
            CancellationToken cancellationToken) =>
        {
            var result = await tenantStore
                .CreateAsync(request.TenantKey, request.DisplayName, cancellationToken)
                .ConfigureAwait(false);

            return result.Status switch
            {
                TenantCreateStatus.Created when result.Tenant is not null =>
                    Results.Created($"/api/tenants/{result.Tenant.TenantKey}", ToResponse(result.Tenant)),
                TenantCreateStatus.AlreadyExists =>
                    Results.Conflict(new { message = $"Tenant '{request.TenantKey}' already exists." }),
                _ => Results.BadRequest()
            };
        }).WithName("Tenants_Create")
            .RequirePermission(BackendPermissionKeys.TenantCreate);

        group.MapPost("/{tenantKey}/activate", async (
            string tenantKey,
            ITenantManagementStore tenantStore,
            CancellationToken cancellationToken) =>
        {
            var result = await tenantStore
                .SetActiveStateAsync(tenantKey, isActive: true, cancellationToken)
                .ConfigureAwait(false);

            return result.Status switch
            {
                TenantSetStateStatus.Updated when result.Tenant is not null => Results.Ok(ToResponse(result.Tenant)),
                TenantSetStateStatus.NotFound => Results.NotFound(new { message = $"Tenant '{tenantKey}' not found." }),
                _ => Results.BadRequest()
            };
        }).WithName("Tenants_Activate")
            .RequirePermission(BackendPermissionKeys.TenantUpdate);

        group.MapPost("/{tenantKey}/suspend", async (
            string tenantKey,
            ITenantManagementStore tenantStore,
            CancellationToken cancellationToken) =>
        {
            var result = await tenantStore
                .SetActiveStateAsync(tenantKey, isActive: false, cancellationToken)
                .ConfigureAwait(false);

            return result.Status switch
            {
                TenantSetStateStatus.Updated when result.Tenant is not null => Results.Ok(ToResponse(result.Tenant)),
                TenantSetStateStatus.NotFound => Results.NotFound(new { message = $"Tenant '{tenantKey}' not found." }),
                _ => Results.BadRequest()
            };
        }).WithName("Tenants_Suspend")
            .RequirePermission(BackendPermissionKeys.TenantUpdate);

        group.MapDelete("/{tenantKey}", async (
            string tenantKey,
            ITenantManagementStore tenantStore,
            CancellationToken cancellationToken) =>
        {
            var result = await tenantStore.RemoveAsync(tenantKey, cancellationToken).ConfigureAwait(false);
            return result.Status switch
            {
                TenantDeleteStatus.Deleted => Results.NoContent(),
                TenantDeleteStatus.NotFound => Results.NotFound(new { message = $"Tenant '{tenantKey}' not found." }),
                TenantDeleteStatus.HasWorkspaces => Results.Conflict(new { message = $"Tenant '{tenantKey}' still has workspaces." }),
                _ => Results.BadRequest()
            };
        }).WithName("Tenants_Delete")
            .RequirePermission(BackendPermissionKeys.TenantDelete);
    }

    private static TenantApiResponse ToResponse(TenantSnapshot snapshot)
    {
        return new TenantApiResponse(
            snapshot.TenantKey,
            snapshot.DisplayName,
            snapshot.IsActive,
            snapshot.CreatedAtUtc,
            snapshot.UpdatedAtUtc);
    }
}
