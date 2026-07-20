using Callora.Core.Application.Extensions;
using Callora.Core.Application.Workspaces;
using Callora.Surface.Rendering.Rendering;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Callora.Surface.Rendering.Api;

/// <summary>
/// The public server-rendered surface route (ADR-015 §7). Resolves the request
/// host/path to a workspace surface, then server-renders that workspace's own
/// template chain: when its primary UI-chain plugin publishes a surface entry
/// (<c>index.njk</c>), the entry is rendered through the confined bundle loader with
/// the full plugin chain in scope, so a real installed template plugin's Nunjucks
/// views (extends/block/include) render at its surface. A workspace that publishes no
/// entry falls back to the built-in SPA shell. Anonymous — the access policy layer
/// (Public/Authenticated/Mixed) is a later phase.
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
                PublishedSurfaceTemplateBundles bundles,
                ILoggerFactory loggerFactory,
                // SSR of the workspace's own plugin chain is opt-in: it needs the full
                // workspace composition (the chain resolver). A headless/minimal host that
                // omits it still serves the SPA shell (E1 behaviour) — hence optional.
                [FromServices] WorkspaceUiChainResolver? chainResolver,
                // The effective theme values are wired in when the theme subsystem is
                // composed; a minimal host without it still renders (unthemed) — optional.
                [FromServices] WorkspacePublicThemeResolver? themeResolver,
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

                // The effective, secret-filtered theme values (defaults + workspace
                // overrides) become allowlisted tokens so a plugin's SSR template can bind
                // {{ tokens.<key> }} onto its --cal-* properties (ADR-015 §8).
                IReadOnlyDictionary<string, string>? effectiveTheme = null;
                if (themeResolver is not null)
                {
                    var theme = await themeResolver
                        .ResolveAsync(workspace.WorkspaceKey, cancellationToken)
                        .ConfigureAwait(false);
                    effectiveTheme = theme?.ValuesByKey;
                }

                var context = new SurfaceRenderContext(
                    TenantKey: workspace.TenantKey,
                    WorkspaceKey: workspace.WorkspaceKey,
                    // Per-surface identity (key/type/locale) is not yet resolved from the
                    // public route — a workspace resolves to its default surface. Distinct
                    // per-surface resolution is a later phase; these stay the defaults.
                    SurfaceKey: "default",
                    SurfaceType: "spa",
                    Locale: "de",
                    Tokens: SurfaceThemeTokens.Compose(
                        workspace.ThemePluginId, workspace.ThemeVersion, effectiveTheme));

                var html = await RenderSurfaceAsync(
                    renderer, chainResolver, bundles, loggerFactory, workspace.WorkspaceKey, context, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Content(html, "text/html; charset=utf-8");
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task<string> RenderSurfaceAsync(
        ISurfaceRenderer renderer,
        WorkspaceUiChainResolver? chainResolver,
        PublishedSurfaceTemplateBundles bundles,
        ILoggerFactory loggerFactory,
        string workspaceKey,
        SurfaceRenderContext context,
        CancellationToken cancellationToken)
    {
        if (chainResolver is null)
        {
            return renderer.Render(SurfaceShellTemplates.SpaRoot, context);
        }

        var chain = await chainResolver
            .ResolveAsync(workspaceKey, cancellationToken)
            .ConfigureAwait(false);

        // The entry belongs to the primary plugin (chain[0]); relative extends/include
        // in it resolve against that plugin's own root, cross-bundle names against the
        // rest of the chain.
        if (chain.Count > 0 && bundles.TryReadEntryTemplate(chain[0]) is { } entryTemplate)
        {
            try
            {
                return renderer.Render(entryTemplate, context, chain);
            }
            catch (SurfaceTemplateException ex)
            {
                // A broken plugin template must not take the whole public surface down:
                // degrade to the SPA shell and make the failure diagnosable.
                loggerFactory
                    .CreateLogger("Callora.Surface.Rendering.SurfaceRender")
                    .LogWarning(
                        ex,
                        "Surface entry template for workspace {WorkspaceKey} (plugin {PluginId}) failed to render; falling back to the SPA shell.",
                        workspaceKey,
                        chain[0]);
            }
        }

        return renderer.Render(SurfaceShellTemplates.SpaRoot, context);
    }
}
