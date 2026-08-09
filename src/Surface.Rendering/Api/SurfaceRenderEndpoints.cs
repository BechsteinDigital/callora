using Callora.Core.Application.Extensions;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Surfaces.Data;
using Callora.Core.Application.Surfaces.Layout;
using Callora.Surface.Rendering.Rendering.Composition;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Surfaces;
using Callora.Surface.Rendering.Rendering;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Callora.Core.Infrastructure.Http;

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
    /// <summary>
    /// Der Direktaufruf: rendert die zum HOST aufgelöste Fläche, ohne dass der Pfad eine
    /// Adresse innerhalb der Fläche wäre. Deshalb greift hier keine Restpfad-Prüfung — sein
    /// eigener Pfad wäre sonst der Rest, den niemand beansprucht.
    /// </summary>
    private const string DirectRenderPath = "/surface/render";

    public static IEndpointRouteBuilder MapSurfaceRenderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        MapSurfaceRoute(endpoints, DirectRenderPath);
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

        // Der Catch-All /{**surfacePath} fängt JEDEN unaufgelösten Pfad — auch /api/…, wenn
        // dort ein Endpunkt fehlt oder der Aufrufer sich vertippt. Ohne diese Prüfung kam
        // darauf 200 mit einer gerenderten Seite und einem gesetzten Surface-Cookie zurück,
        // statt 404. Ein 200 mit falschem Inhalt ist die unangenehmste Sorte Fehler: Der
        // Aufrufer meldet einen Parse-Fehler, und niemand sucht beim Routing. Genau so blieb
        // ein falscher API-Pfad im Composer-Bundle unsichtbar, bis jemand die Oberfläche
        // öffnete.
        // Der Workspace-Catch-All hatte diese Abgrenzung von Anfang an; in einer colocated
        // Komposition gewinnt aber dieser hier.
        if (PlatformOwnedPathSegments.IsPlatformOwned(path))
        {
            return Results.NotFound();
        }

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

        // Theme und Komposition ZUERST, vor allem, was den Aufrufer betrifft.
        //
        // Sie hängen nicht an ihm — wohl aber hängt an ihnen, WER auf dieser Fläche überhaupt
        // beitragen darf: Eine Inhaltsfläche zeigt, was ihr Layout verlangt, und das steht erst
        // fest, wenn die Komposition geladen ist. Standen sie weiter unten, kannte die
        // Slot-Auflösung das Layout noch nicht und ließ jedes aktive Plugin durch.
        // The effective, secret-filtered theme values (defaults + workspace
        // overrides) become allowlisted tokens so a plugin's SSR template can bind
        // {{ tokens.<key> }} onto its --cal-* properties (ADR-015 §8).
        IReadOnlyDictionary<string, string>? effectiveTheme = null;
        // Auch die Sektionslayouts des Themes: Der Kompositions-Renderer muss erkennen, dass ein
        // im Dokument gespeichertes Layout nach einem Theme-Wechsel niemanden mehr hat, der es
        // stylen kann (§7.8).
        WorkspacePublicTheme? resolvedTheme = null;
        if (themeResolver is not null)
        {
            // Resolved for THIS surface: its own theme and values win over the
            // workspace's. Previously only the workspace values were read, so a
            // surface with its own theme rendered that theme's identity with the
            // workspace theme's values.
            resolvedTheme = await themeResolver
                .ResolveForSurfaceAsync(surface.WorkspaceKey, surface.SurfaceKey, cancellationToken)
                .ConfigureAwait(false);
            effectiveTheme = resolvedTheme?.ValuesByKey;
        }

        // Aus dem PLUGIN-KATALOG, nicht aus dem Host-Container.
        //
        // Der Composer exportiert die Layout-Quelle über `context.Export<>()` — sie landet damit
        // im Katalog, nicht in den RequestServices. `GetService` fand sie deshalb nie: Eine
        // veröffentlichte Seite lag in der Datenbank, und der Renderpfad lieferte trotzdem die
        // leere SPA-Shell. Kein Fehler, kein Log — nur eine Seite, auf der nichts von dem steht,
        // was jemand gebaut hat.
        //
        // Der Slot-Resolver daneben fragte längst den Katalog. Zwei Wege für dieselbe Art
        // Dienst, und nur einer davon funktionierte.
        var catalog = httpContext.RequestServices.GetService<ICalloraPluginCatalog>();
        var exports = catalog?.GetExports(typeof(ISurfaceLayoutSource)) ?? [];
        var layouts = exports.OfType<ISurfaceLayoutSource>().FirstOrDefault()
            ?? httpContext.RequestServices.GetService<ISurfaceLayoutSource>();

        // MESSUNG statt Vermutung: Eine veröffentlichte Seite kam nicht an, und drei Hypothesen
        // dazu waren falsch. Diese Zeile sagt, ob die Ablage leer ist oder der Typ nicht passt —
        // zwei Fälle, die sich ohne sie nicht unterscheiden lassen.
        if (layouts is null)
        {
            loggerFactory
                .CreateLogger("Callora.Surface.Rendering.SurfaceRender")
                .LogWarning(
                    "Keine Layout-Quelle für {Workspace}/{Surface}: Katalog {CatalogState}, "
                    + "{ExportCount} Exporte für {Contract} (aus {ContractAssembly}). Gefunden: {Found}.",
                    surface.WorkspaceKey,
                    surface.SurfaceKey,
                    catalog is null ? "fehlt" : catalog.GetType().Name,
                    exports.Count,
                    typeof(ISurfaceLayoutSource).FullName,
                    typeof(ISurfaceLayoutSource).Assembly.FullName,
                    string.Join(", ", exports.Select(export => export.GetType().FullName)));
        }
        var composed = layouts is null
            ? default
            : await RenderCompositionAsync(
                    layouts,
                    surface,
                    resolvedTheme,
                    loggerFactory.CreateLogger("Callora.Surface.Rendering.SurfaceRender"),
                    cancellationToken)
                .ConfigureAwait(false);
        var composition = composed.Html;
        var usedBlockIds = composed.BlockIds ?? [];


        SurfaceCallerView? caller = null;
        // Die Template-Sicht oben lässt die Identität bewusst weg; ein Contributor braucht sie,
        // um seine Antwort zu formen — nicht, um über Zugriff zu entscheiden.
        SurfaceCaller? establishedCaller = null;
        var compositionSlots = SurfaceComposition.Empty;
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
            establishedCaller = establishment.Caller;

            // Sichtbarkeit je Knoten (ADR-019 §4). `RequiredClaims` trägt hier die Anforderung
            // der GANZEN Kette — was ein Elternteil verlangt, gilt auch für die Unterseite, die
            // eine eigene URL hat.
            //
            // 404 und nicht 403: Ein Knoten, den dieser Besucher nicht sehen darf, verhält sich
            // wie einer, den es nicht gibt. Ein 403 verriete seine Existenz, und genau das ist
            // bei einer Gliederung oft die Information, die niemanden angeht.
            if (!SurfaceVisibility.Satisfies(
                    surface.RequiredClaims,
                    // Die Fläche gewährt mit: Ein Gast hat sonst nie einen Claim.
                    SurfaceVisibility.ClaimsOn(establishment.Caller, surface.GrantedClaims)))
            {
                return Results.NotFound();
            }

            // Wer auf dieser Fläche beitragen darf: die UI-Kette. Ohne diese Grenze steuerte
            // jedes im Workspace aktive Plugin seine Navigation zu JEDER Fläche bei — die
            // Videokonferenz stand im Menü einer Inhaltsseite, die sie nie erwähnt.
            var contributors = chainResolver is null
                ? null
                : SurfaceContributors.OnThisSurface(
                    await chainResolver
                        .ResolveAsync(surface.WorkspaceKey, surface.SurfaceKey, cancellationToken)
                        .ConfigureAwait(false),
                    surface,
                    usedBlockIds);

            // Resolved per request because it depends on the caller: claim-gated views
            // are filtered here, not hidden in the browser.
            if (slotResolver is not null)
            {
                compositionSlots = await slotResolver
                    .ResolveAsync(
                        surface.WorkspaceKey,
                        surface.SurfaceKey,
                        establishment.Caller,
                        surface.GrantedClaims,
                        // Dieselbe Kette, die die Bundles bestimmt: Was nicht geladen wird, darf
                        // auch nicht rendern. Zwei Listen wären zwei Antworten auf dieselbe Frage.
                        contributors,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        else
        {
            // Zusammenstellung ohne Identitäts-Subsystem: Es gibt keine Claims, also ist ein
            // Knoten, der welche verlangt, hier nicht erreichbar. Ihn durchzulassen wäre die
            // gefährlichste Variante — die Anforderung stünde in der Verwaltung und wirkte nicht.
            if (SurfaceVisibility.Parse(surface.RequiredClaims).Count > 0)
            {
                return Results.NotFound();
            }

            if (surface.Authentication.RequiresSignIn() &&
                httpContext.User.Identity?.IsAuthenticated != true)
            {
                // Ansonsten bleibt das Verhalten vor ADR-017 exakt wie es war, damit ein
                // bestehender Host unberührt bleibt.
                return SurfaceAccessGate.LoginRedirect(surface, httpContext);
            }
        }

        // Everything a contributor needs to tell one page from another. The prefix comes off
        // here rather than in every contributor: the first one to get it wrong on a surface
        // mounted at "/" would never find out.
        var surfacePath = StripPrefix(path, surface.PublicPathPrefix);

        var data = SurfaceDataComposition.Empty;
        if (httpContext.RequestServices.GetService<SurfaceDataResolver>() is { } dataResolver)
        {
            data = await dataResolver
                .ResolveAsync(
                    new SurfaceDataRequest(
                        surface.WorkspaceKey, surface.SurfaceKey, surfacePath, locale, establishedCaller),
                    surface.Authentication,
                    cancellationToken)
                .ConfigureAwait(false);

            // Zwei verschiedene Antworten, und nur der Contributor konnte sie unterscheiden:
            // "das gibt es nicht" ist eine 404, "ich kam nicht heran" eine 503. Eine halbe
            // Seite auszuliefern wäre schlimmer als beide — sie sähe vollständig aus.
            if (data.MissingRequiredNamespace is not null)
            {
                return Results.NotFound();
            }

            if (data.FailedRequiredNamespace is not null)
            {
                loggerFactory
                    .CreateLogger("Callora.Surface.Rendering.SurfaceRender")
                    .LogError(
                        "Required surface data contributor {Namespace} could not answer for {Surface}{Path}.",
                        data.FailedRequiredNamespace,
                        surface.SurfaceKey,
                        surfacePath);
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            // Caller-abhängige Daten stehen im HTML. Ein Proxy davor lieferte sonst die Daten
            // des ersten Besuchers an alle danach — der leise Fehler dieses Musters.
            if (!data.Cacheable)
            {
                httpContext.Response.Headers.CacheControl = "no-store";
            }
        }

        // Resolved from the container rather than bound as a parameter: [FromServices] on an
        // unregistered INTERFACE fails the binding and answers 400, where the intent is "no
        // composer installed, carry on".
        //
        // GetPublishedAsync, and only that. There is no ?preview=true and no header that would
        // reach a draft from here — on a Public surface such a hole would sit behind no
        // authentication at all (design §7.3).

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
            Path = surfacePath,
            Data = data.Values,
            Slots = compositionSlots.Slots,
            Navigation = compositionSlots.Navigation,
            CompositionHtml = composition,
        };

        // Ein Restpfad gehört der Fläche nur, wenn sie ihn BEANSPRUCHT (ADR-022).
        //
        // Die Auflösung nimmt das längste passende Präfix; was dahinter stand, fiel unter den
        // Tisch. `/test/blub/gibtsnicht` antwortete mit 200 und dem Inhalt von `/test/blub` —
        // dieselbe Klasse wie ein `/api/`-Pfad, der eine Flächenseite bekam: kein Statuscode,
        // kein Log, und die Suche beginnt an der falschen Stelle.
        //
        // Entschieden wird es an der FLÄCHE, nicht am Renderweg. Ob ein Plugin ein eigenes
        // Server-Template mitbringt, sagt nichts über Adressierung: Ein Template ist kein Router,
        // und eine Anwendung mit History-Routing braucht durchgereichte Unterpfade ganz ohne.
        //
        // /surface/render ist ausgenommen: Der Direktaufruf löst über den HOST auf, sein eigener
        // Pfad wäre sonst der Rest, den niemand beansprucht.
        if (surface.Routing is not SurfaceRouting.Application &&
            !string.Equals(path, DirectRenderPath, StringComparison.OrdinalIgnoreCase) &&
            SurfaceRouteRemainder.Of(surface.PublicPathPrefix, path) is not "")
        {
            return Results.NotFound();
        }

        var shell = await ChooseShellAsync(
                chainResolver,
                bundles,
                surface.WorkspaceKey,
                surface.SurfaceKey,
                composition is not null,
                cancellationToken)
            .ConfigureAwait(false);

        var html = RenderSurface(renderer, loggerFactory, shell, surface.WorkspaceKey, context);
        return Results.Content(html, "text/html; charset=utf-8");
    }

    /// <summary>
    /// The path within the surface. A surface mounted at <c>/shop</c> turns
    /// <c>/shop/produkt/schuhe</c> into <c>/produkt/schuhe</c>; one mounted at <c>/</c> changes
    /// nothing.
    /// </summary>
    /// <remarks>
    /// Rechnet nicht selbst: Dieselbe Zerlegung entscheidet auch, ob ein Restpfad überhaupt
    /// bedient wird. Zwei Implementierungen wären zwei Antworten auf dieselbe Frage — und die
    /// hier war bereits die falsche, weil sie am Zeichen statt an der Segmentgrenze verglich
    /// und <c>/test/blubber</c> als <c>/test/blub</c> plus <c>ber</c> las.
    /// </remarks>
    private static string StripPrefix(string path, string? prefix) =>
        SurfaceRouteRemainder.Of(prefix, path) switch
        {
            "" => "/",
            var rest => "/" + rest,
        };

    private static async Task<(string? Html, IReadOnlyCollection<string> BlockIds)> RenderCompositionAsync(
        ISurfaceLayoutSource layouts,
        WorkspaceSurfaceSnapshot surface,
        WorkspacePublicTheme? theme,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var document = await layouts
            .GetPublishedAsync(surface.WorkspaceKey, surface.SurfaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            // Zweiter Messpunkt: Die Quelle ist da (sonst hätte der erste angeschlagen), das
            // Dokument aber nicht. Damit steht fest, dass es an der ABFRAGE liegt und nicht an
            // der Diensterkennung — zwei Fälle, die sich sonst nicht unterscheiden lassen.
            logger?.LogWarning(
                "Keine veröffentlichte Komposition für {Workspace}/{Surface} aus {Source}.",
                surface.WorkspaceKey,
                surface.SurfaceKey,
                layouts.GetType().FullName);
            return (null, []);
        }

        logger?.LogInformation(
            "Komposition für {Workspace}/{Surface}: {SectionCount} Sektionen.",
            surface.WorkspaceKey,
            surface.SurfaceKey,
            document.Sections.Count);

        // Was gilt: die Layouts des Themes, oder — ohne zugewiesenes Theme — die des
        // Basis-Themes. Die Liste ist nie leer, denn ein Theme ohne eigene Layouts erbt die
        // Basis; nur ein Theme, das ausdrücklich nicht erbt, engt sie ein.
        //
        // Deshalb kann ein Rückfall hier nur eines heißen: Dieses Layout kennt niemand mehr, der
        // es stylen könnte (§7.8) — und nicht „es hat nur gerade niemand etwas dazu gesagt".
        var knownLayouts = new HashSet<string>(
            (theme?.SectionLayouts ?? SurfaceBaseSectionLayouts.All).Select(layout => layout.LayoutKey),
            StringComparer.Ordinal);

        return (
            new SurfaceCompositionRenderer(layoutIsKnown: knownLayouts.Contains).Render(document),
            document.Sections.SelectMany(section => section.Blocks).Select(block => block.BlockId).ToArray());
    }

    /// <summary>
    /// Which shell renders this surface. The order is a decision, not an accident:
    /// <list type="number">
    /// <item>A COMPOSED layout wins. Somebody published it for this surface deliberately; a
    /// template that quietly overrode it would make the editor unreliable.</item>
    /// <item>Then a plugin's own SSR entry — a developer writing index.njk takes the surface
    /// over, which is what that file means.</item>
    /// <item>Otherwise the built-in shell, which is itself the host's base bundle.</item>
    /// </list>
    /// </summary>
    private static async Task<SurfaceShell> ChooseShellAsync(
        WorkspaceUiChainResolver? chainResolver,
        PublishedSurfaceTemplateBundles bundles,
        string workspaceKey,
        string surfaceKey,
        bool hasComposition,
        CancellationToken cancellationToken)
    {
        if (hasComposition)
        {
            return new SurfaceShell(SurfaceShellTemplates.Composed, []);
        }

        if (chainResolver is null)
        {
            return new SurfaceShell(SurfaceShellTemplates.SpaRoot, []);
        }

        var chain = await chainResolver
            .ResolveAsync(workspaceKey, surfaceKey, cancellationToken)
            .ConfigureAwait(false);

        // The entry belongs to the primary plugin (chain[0]); relative extends/include
        // in it resolve against that plugin's own root, cross-bundle names against the
        // rest of the chain.
        if (chain.Count > 0 && bundles.TryReadEntryTemplate(chain[0]) is { } entryTemplate)
        {
            return new SurfaceShell(entryTemplate, chain);
        }

        return new SurfaceShell(SurfaceShellTemplates.SpaRoot, []);
    }

    private static string RenderSurface(
        ISurfaceRenderer renderer,
        ILoggerFactory loggerFactory,
        SurfaceShell shell,
        string workspaceKey,
        SurfaceRenderContext context)
    {
        try
        {
            return renderer.Render(shell.Template, context, shell.Chain);
        }
        catch (SurfaceTemplateException ex)
        {
            // A broken plugin template must not take the whole public surface down:
            // degrade to the SPA shell and make the failure diagnosable.
            //
            loggerFactory
                .CreateLogger("Callora.Surface.Rendering.SurfaceRender")
                .LogWarning(
                    ex,
                    "Surface entry template for workspace {WorkspaceKey} (plugin {PluginId}) failed to render; falling back to the SPA shell.",
                    workspaceKey,
                    shell.Chain.Count > 0 ? shell.Chain[0] : "-");

            return renderer.Render(SurfaceShellTemplates.SpaRoot, context);
        }
    }
}
