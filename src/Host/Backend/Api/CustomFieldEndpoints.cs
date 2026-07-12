using Callora.Host.Backend.Application.Abstractions.CustomFields;
using Callora.Host.Backend.Infrastructure.Security;

namespace Callora.Host.Backend.Api;

/// <summary>
/// Custom field definitions and values on core entities. Workspace-entity
/// access is workspace-scope enforced (entityId = workspace key).
/// </summary>
public static class CustomFieldEndpoints
{
    public static IEndpointRouteBuilder MapCustomFieldEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/custom-fields")
            .RequireAuthorization();

        group.MapGet("/definitions", async (
                ICustomFieldStore store,
                string? entityName,
                CancellationToken cancellationToken) =>
                Results.Ok(await store.ListDefinitionsAsync(entityName, cancellationToken)))
            .RequirePermission(BackendPermissionKeys.CustomFieldRead);

        group.MapGet("/{entityName}/{entityId}", async (
                ICustomFieldStore store,
                string entityName,
                string entityId,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!HasEntityAccess(httpContext, entityName, entityId))
                    return Results.Forbid();

                return Results.Ok(await store.GetValuesAsync(entityName, entityId, cancellationToken));
            })
            .RequirePermission(BackendPermissionKeys.CustomFieldRead);

        group.MapPut("/{entityName}/{entityId}", async (
                ICustomFieldStore store,
                string entityName,
                string entityId,
                UpsertCustomFieldValuesApiRequest request,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!HasEntityAccess(httpContext, entityName, entityId))
                    return Results.Forbid();

                var values = (request.ValuesByKey ?? []).ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value?.GetRawText(),
                    StringComparer.OrdinalIgnoreCase);
                await store.SetValuesAsync(entityName, entityId, values, cancellationToken);
                return Results.NoContent();
            })
            .RequirePermission(BackendPermissionKeys.CustomFieldUpdate);

        return app;
    }

    private static bool HasEntityAccess(HttpContext httpContext, string entityName, string entityId)
    {
        // For workspace entities the id IS the workspace key; other entities
        // carry no per-entity ownership yet, so they stay operator-only.
        return string.Equals(entityName?.Trim(), "workspace", StringComparison.OrdinalIgnoreCase)
            ? WorkspaceScopeEvaluator.HasWorkspaceAccess(httpContext.User, entityId)
            : WorkspaceScopeEvaluator.IsOperator(httpContext.User);
    }
}
