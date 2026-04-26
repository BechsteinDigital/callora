using System.Security.Claims;
using Callora.Host.Backend.Application.Abstractions.Extensions;
using Callora.Host.Backend.Application.Abstractions.Workspaces;
using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Infrastructure.Security;

namespace Callora.Host.Workspace.Api;

public static class WorkspaceThemeEndpoints
{
    public static void MapWorkspaceThemeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/workspace/themes")
            .WithTags("Workspace Themes")
            .RequireAuthorization();

        group.MapGet("/effective", async (
            string? workspaceKey,
            ClaimsPrincipal user,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            IWorkspaceTemplateResolutionService resolver,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hostOptions.DefaultTenantKey))
            {
                return Results.BadRequest(new { message = "Workspace host default tenant key is not configured." });
            }

            var resolvedWorkspaceKey = string.IsNullOrWhiteSpace(workspaceKey)
                ? WorkspaceClaims.ResolveWorkspaceKey(user)
                : workspaceKey.Trim();

            if (string.IsNullOrWhiteSpace(resolvedWorkspaceKey))
            {
                return Results.BadRequest(new { message = "workspaceKey query parameter or workspace claim is required." });
            }

            var workspace = await workspaceStore.GetAsync(resolvedWorkspaceKey, cancellationToken).ConfigureAwait(false);
            if (workspace is null ||
                !string.Equals(workspace.TenantKey, hostOptions.DefaultTenantKey, StringComparison.OrdinalIgnoreCase))
            {
                return Results.NotFound(new { message = $"Workspace '{resolvedWorkspaceKey}' not found." });
            }

            var effective = await resolver.ResolveAsync(resolvedWorkspaceKey, cancellationToken).ConfigureAwait(false);
            var response = effective.Select(x => new WorkspaceTemplateEffectiveApiResponse(
                    x.TenantKey,
                    x.WorkspaceKey,
                    x.TemplateKey,
                    x.Surface,
                    x.PluginId,
                    x.Version,
                    x.DisplayName,
                    x.TemplatePath,
                    x.ParentTemplateKey,
                    x.Scope,
                    x.Source,
                    x.Priority))
                .ToArray();
            return Results.Ok(response);
        }).WithName("WorkspaceThemes_Effective")
            .RequirePermission(BackendPermissionKeys.ExtensionRead);
    }
}
