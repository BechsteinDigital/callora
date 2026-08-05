using Callora.Core.Application.Extensions;
using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Surfaces;
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
/// access mode (Public/Authenticated/Mixed) against the established caller
/// (ADR-017 §6.1): Public and Mixed render anonymously, an Authenticated surface
/// refuses without an identity, and one whose assigned identity provider cannot be
/// consulted closes rather than degrading to anonymous.
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
        // Identity is composed with the surface session subsystem; a host without it
        // keeps the pre-ADR-017 behaviour (backend principal or anonymous) — optional.
        [FromServices] SurfaceRequestCallerResolver? callerResolver,
        // Slot composition is opt-in with the plugin catalog; a host without it renders
        // the template with empty slots rather than failing (#125 block C).
        [FromServices] SurfaceSlotResolver? slotResolver,
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

        var locale = string.IsNullOrWhiteSpace(surface.Locale) ? "de" : surface.Locale;

        SurfaceCallerView? caller = null;
        var composition = SurfaceComposition.Empty;
        if (callerResolver is not null)
        {
            var establishment = await callerResolver
                .EstablishAsync(httpContext, surface, locale, cancellationToken)
                .ConfigureAwait(false);

            if (SurfaceAccessGate.Reject(surface, establishment, httpContext) is { } rejection)
            {
                return rejection;
            }

            caller = SurfaceCallerViewFactory.Create(establishment.Caller);

            // Resolved per request because it depends on the caller: claim-gated views
            // are filtered here, not hidden in the browser.
            if (slotResolver is not null)
            {
                composition = await slotResolver
                    .ResolveAsync(
                        surface.WorkspaceKey, surface.SurfaceKey, establishment.Caller, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        else if (surface.AccessMode == SurfaceAccessMode.Authenticated &&
                 httpContext.User.Identity?.IsAuthenticated != true)
        {
            // Composition without the identity subsystem: the pre-ADR-017 behaviour
            // stays exactly as it was, so an existing host is unaffected.
            return SurfaceAccessGate.LoginRedirect(surface, httpContext);
        }

        // The effective, secret-filtered theme values (defaults + workspace
        // overrides) become allowlisted tokens so a plugin's SSR template can bind
        // {{ tokens.<key> }} onto its --cal-* properties (ADR-015 §8).
        IReadOnlyDictionary<string, string>? effectiveTheme = null;
        if (themeResolver is not null)
        {
            // Resolved for THIS surface: its own theme and values win over the
            // workspace's. Previously only the workspace values were read, so a
            // surface with its own theme rendered that theme's identity with the
            // workspace theme's values.
            var theme = await themeResolver
                .ResolveForSurfaceAsync(surface.WorkspaceKey, surface.SurfaceKey, cancellationToken)
                .ConfigureAwait(false);
            effectiveTheme = theme?.ValuesByKey;
        }

        var context = new SurfaceRenderContext(
            TenantKey: surface.TenantKey,
            WorkspaceKey: surface.WorkspaceKey,
            SurfaceKey: surface.SurfaceKey,
            SurfaceType: surface.SurfaceType,
            Locale: locale,
            Tokens: SurfaceThemeTokens.Compose(
                surface.ThemePluginId, surface.ThemeVersion, effectiveTheme))
        {
            Caller = caller,
            Slots = composition.Slots,
            Navigation = composition.Navigation,
        };

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
