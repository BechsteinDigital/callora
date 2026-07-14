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
                ISystemConfigStore store,
                string pluginId,
                string? workspaceKey,
                CancellationToken cancellationToken) =>
            {
                var values = await resolver.ResolveAsync(pluginId, tenantKey: null, workspaceKey, cancellationToken);

                // Secrets are write-only through the API: internal consumers
                // (mail, plugins) resolve plaintext, clients read "***".
                var definitions = await store.ListDefinitionsAsync(pluginId, cancellationToken);
                var secretKeys = definitions
                    .Where(definition => SystemConfigFieldTypes.IsSecret(definition.FieldType))
                    .Select(definition => definition.ConfigKey)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var valuesByKey = values.ToDictionary(
                    pair => pair.Key,
                    pair => secretKeys.Contains(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value)
                        ? "\"***\""
                        : pair.Value,
                    StringComparer.OrdinalIgnoreCase);

                return Results.Ok(new { pluginId, workspaceKey, valuesByKey });
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
                    return ApiProblems.BadRequest("pluginId and a valid scope (global|tenant|workspace) are required.");

                if (request.Scope != SystemConfigScopes.Global && string.IsNullOrWhiteSpace(request.ScopeKey))
                    return ApiProblems.BadRequest("scopeKey is required for tenant and workspace scope.");

                // Explicit per-scope authorization (the scope key travels in
                // the body): global and tenant values are operator-only;
                // workspace values require access to that workspace.
                var allowed = request.Scope == SystemConfigScopes.Workspace
                    ? WorkspaceScopeEvaluator.HasWorkspaceAccess(httpContext.User, request.ScopeKey)
                    : WorkspaceScopeEvaluator.IsOperator(httpContext.User);
                if (!allowed)
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
