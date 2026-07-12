using Callora.Host.Backend.Application.Abstractions.Configuration;
using Callora.Host.Backend.Application.Configuration;
using Callora.Host.Backend.Infrastructure.Security;

namespace Callora.Host.Backend.Api;

/// <summary>
/// Scoped system configuration: definitions come from plugin config schemas,
/// values resolve workspace &gt; tenant &gt; global &gt; default.
/// </summary>
public static class SystemConfigEndpoints
{
    public static IEndpointRouteBuilder MapSystemConfigEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/config")
            .RequireAuthorization();

        group.MapGet("/definitions", async (
                ISystemConfigStore store,
                string? pluginId,
                CancellationToken cancellationToken) =>
                Results.Ok(await store.ListDefinitionsAsync(pluginId, cancellationToken)))
            .RequirePermission(BackendPermissionKeys.ConfigRead);

        group.MapGet("/effective", async (
                SystemConfigResolver resolver,
                string pluginId,
                string? workspaceKey,
                CancellationToken cancellationToken) =>
            {
                var values = await resolver.ResolveAsync(pluginId, tenantKey: null, workspaceKey, cancellationToken);
                return Results.Ok(new { pluginId, workspaceKey, valuesByKey = values });
            })
            .RequirePermission(BackendPermissionKeys.ConfigRead)
            .RequireWorkspaceScope();

        group.MapPut("/values", async (
                ISystemConfigStore store,
                UpsertSystemConfigValuesApiRequest request,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.PluginId) || !SystemConfigScopes.IsValid(request.Scope))
                    return Results.BadRequest(new { error = "pluginId and a valid scope (global|tenant|workspace) are required." });

                if (request.Scope != SystemConfigScopes.Global && string.IsNullOrWhiteSpace(request.ScopeKey))
                    return Results.BadRequest(new { error = "scopeKey is required for tenant and workspace scope." });

                // The scope key travels in the body, so workspace binding is
                // enforced here instead of via RequireWorkspaceScope.
                var requestedWorkspace = request.Scope == SystemConfigScopes.Workspace ? request.ScopeKey : null;
                if (!WorkspaceScopeEvaluator.HasWorkspaceAccess(httpContext.User, requestedWorkspace))
                    return Results.Forbid();

                var values = (request.ValuesByKey ?? []).ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value?.GetRawText(),
                    StringComparer.OrdinalIgnoreCase);

                await store.UpsertValuesAsync(
                    request.PluginId,
                    request.Scope,
                    request.ScopeKey ?? string.Empty,
                    values,
                    cancellationToken);
                return Results.NoContent();
            })
            .RequirePermission(BackendPermissionKeys.ConfigUpdate);

        return app;
    }
}
