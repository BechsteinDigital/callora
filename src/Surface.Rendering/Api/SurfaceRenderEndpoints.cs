using Callora.Core.Application.Extensions;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
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
/// entry falls back to the built-in SPA shell. The resolved surface is gated on its own
/// access mode (Public/Authenticated/Mixed): an Authenticated surface redirects an
/// anonymous caller to log in, Public and Mixed render anonymously.
/// </summary>
public static class SurfaceRenderEndpoints
{
    public static IEndpointRouteBuilder MapSurfaceRenderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        MapSurfaceRoute(endpoints, "/surface/render");
        MapSurfaceRoute(endpoints, "/{**surfacePath:nonfile}");

        // Both Workspace and Surface.Rendering expose an exact root route. In a
        // colocated composition Surface.Rendering is the shell and must win that
        // tie; a host that intentionally uses an external Workspace shell simply
        // omits this module and retains Workspace's redirect behaviour.
        MapSurfaceRoute(endpoints, "/", order: -100);

        return endpoints;
    }

    private static void MapSurfaceRoute(
        IEndpointRouteBuilder endpoints,
        string pattern,
        int? order = null)
    {
        var route = endpoints
            .MapGet(pattern, RenderEndpointAsync)
            .AllowAnonymous()
            .ExcludeFromDescription();
        if (order is not null)
        {
            route.WithOrder(order.Value);
        }
    }

    private static async Task<IResult> RenderEndpointAsync(
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
        CancellationToken cancellationToken)
    {
        var host = httpContext.Request.Host.Host;
        var path = httpContext.Request.Path.HasValue ? httpContext.Request.Path.Value! : "/";

        var surface = await workspaceStore
            .ResolveSurfaceByPublicRouteAsync(host, path, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        // A non-null result is already guaranteed active: the store's matching loop
        // skips inactive surfaces, workspaces and tenants — no re-check needed here.
        if (surface is null)
        {
            return Results.NotFound();
        }

        // Access mode (ADR-014 §6.1) is gated per surface. Authenticated redirects an
        // anonymous caller to log in; Public and Mixed serve the shell anonymously
        // (Mixed's per-route protection is not this endpoint's concern). This is the
        // authoritative boundary; client-side UI hiding is never a substitute.
        if (surface.AccessMode == SurfaceAccessMode.Authenticated &&
            httpContext.User.Identity?.IsAuthenticated != true)
        {
            var returnUrl = httpContext.Request.Path + httpContext.Request.QueryString;
            return Results.Redirect(
                $"/login?workspaceKey={Uri.EscapeDataString(surface.WorkspaceKey)}" +
                $"&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        // The effective, secret-filtered theme values (defaults + workspace
        // overrides) become allowlisted tokens so a plugin's SSR template can bind
        // {{ tokens.<key> }} onto its --cal-* properties (ADR-015 §8).
        IReadOnlyDictionary<string, string>? effectiveTheme = null;
        if (themeResolver is not null)
        {
            var theme = await themeResolver
                .ResolveAsync(surface.WorkspaceKey, cancellationToken)
                .ConfigureAwait(false);
            effectiveTheme = theme?.ValuesByKey;
        }

        var context = new SurfaceRenderContext(
            TenantKey: surface.TenantKey,
            WorkspaceKey: surface.WorkspaceKey,
            SurfaceKey: surface.SurfaceKey,
            SurfaceType: surface.SurfaceType,
            Locale: string.IsNullOrWhiteSpace(surface.Locale) ? "de" : surface.Locale,
            Tokens: SurfaceThemeTokens.Compose(
                surface.ThemePluginId, surface.ThemeVersion, effectiveTheme));

        var html = await RenderSurfaceAsync(
                renderer,
                chainResolver,
                bundles,
                loggerFactory,
                surface.WorkspaceKey,
                context,
                cancellationToken)
            .ConfigureAwait(false);
        return Results.Content(html, "text/html; charset=utf-8");
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
            .ResolveAsync(workspaceKey, context.SurfaceKey, cancellationToken)
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
