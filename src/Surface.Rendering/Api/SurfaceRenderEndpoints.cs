using Callora.Core.Application.Workspaces;

namespace Callora.Surface.Rendering.Api;

/// <summary>
/// The public server-rendered surface route (ADR-015 §7, phase E1). Resolves the
/// request host/path to a workspace surface and renders the SurfaceShell to HTML.
/// Anonymous — the access policy layer (Public/Authenticated/Mixed) is a later
/// phase; E1 serves the SPA-root document only.
/// </summary>
public static class SurfaceRenderEndpoints
{
    public static IEndpointRouteBuilder MapSurfaceRenderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/surface/render", async (
                HttpContext httpContext,
                IWorkspaceManagementStore workspaceStore,
                ISurfaceRenderer renderer,
                CancellationToken cancellationToken) =>
            {
                var host = httpContext.Request.Host.Host;
                var path = httpContext.Request.Path.HasValue ? httpContext.Request.Path.Value! : "/";

                var workspace = await workspaceStore
                    .ResolveByPublicRouteAsync(host, path, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (workspace is null || !workspace.IsActive || !workspace.TenantIsActive)
                {
                    return Results.NotFound();
                }

                var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
                if (!string.IsNullOrWhiteSpace(workspace.ThemePluginId))
                {
                    tokens["themePluginId"] = workspace.ThemePluginId;
                }

                var context = new SurfaceRenderContext(
                    TenantKey: workspace.TenantKey,
                    WorkspaceKey: workspace.WorkspaceKey,
                    SurfaceKey: "default",
                    SurfaceType: "spa",
                    Locale: "de",
                    Tokens: tokens);

                var html = renderer.Render(SurfaceShellTemplates.SpaRoot, context);
                return Results.Content(html, "text/html; charset=utf-8");
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        return endpoints;
    }
}
