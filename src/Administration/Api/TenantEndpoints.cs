using Callora.Core.Api;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Security;
using Callora.Core.Application.Tenants;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Callora.Administration.Api;

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
                    ApiProblems.Conflict($"Tenant '{request.TenantKey}' already exists."),
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
                TenantSetStateStatus.NotFound => ApiProblems.NotFound($"Tenant '{tenantKey}' not found."),
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
                TenantSetStateStatus.NotFound => ApiProblems.NotFound($"Tenant '{tenantKey}' not found."),
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
                TenantDeleteStatus.NotFound => ApiProblems.NotFound($"Tenant '{tenantKey}' not found."),
                TenantDeleteStatus.HasWorkspaces => ApiProblems.Conflict($"Tenant '{tenantKey}' still has workspaces."),
                _ => Results.BadRequest()
            };
        }).WithName("Tenants_Delete")
            .RequirePermission(BackendPermissionKeys.TenantDelete);

        // Die dritte Stufe des Bezugs. Der Instanzbetreiber entscheidet per Entitlement, WAS ein
        // Mandant nutzen darf; der Mandant weist es seinen Workspaces zu. Hier gibt er die Zuweisung
        // für einzelne Plugins an die Workspace-Administratoren ab — oder holt sie zurück.
        group.MapGet("/{tenantKey}/plugins/delegations", async (
            string tenantKey,
            [FromServices] ITenantPluginDelegationStore delegations,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!WorkspaceScopeEvaluator.HasTenantAccess(httpContext.User, tenantKey))
            {
                return Results.Forbid();
            }

            var delegated = await delegations
                .ListDelegatedAsync(tenantKey, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(delegated);
        }).WithName("Tenants_ListPluginDelegations")
            .RequirePermission(BackendPermissionKeys.PluginAssign);

        group.MapPut("/{tenantKey}/plugins/{pluginId}/delegation", async (
            string tenantKey,
            string pluginId,
            SetTenantPluginDelegationApiRequest request,
            [FromServices] ITenantPluginDelegationStore delegations,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // Geprüft wird der Mandant aus der URL gegen den der Sitzung, nicht nur das Recht: Ein
            // Mandanten-Administrator trägt plugin.assign, und ohne diese Zeile setzte er die
            // Delegation des Nachbarn, indem er dessen Schlüssel in den Pfad schreibt.
            if (!WorkspaceScopeEvaluator.HasTenantAccess(httpContext.User, tenantKey))
            {
                return Results.Forbid();
            }

            await delegations
                .SetAsync(
                    tenantKey,
                    pluginId,
                    request.WorkspacesMayAssign,
                    httpContext.User.FindFirstValue("sub") ?? httpContext.User.Identity?.Name,
                    cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new TenantPluginDelegationApiResponse(
                pluginId, request.WorkspacesMayAssign));
        }).WithName("Tenants_SetPluginDelegation")
            .RequirePermission(BackendPermissionKeys.PluginAssign);
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
