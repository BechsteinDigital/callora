using Callora.Core.Api;
using Callora.Core.Application.Extensions;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text.Json;

namespace Callora.Administration.Api;

public static class PluginAdminExtensionEndpoints
{
    public static void MapPluginAdminExtensionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ext/admin")
            .WithTags("Plugin Extensions")
            .RequireAuthorization();

        // Navigation is deliberately readable for every authenticated session:
        // entries carrying a RequiredPermission are filtered per user below.
        group.MapGet("/navigation", (HttpContext httpContext, ICalloraPluginCatalog pluginCatalog) =>
        {
            var contributors = pluginCatalog.GetExports<IHostAdminApiExtensionContributor>();
            var items = contributors
                .SelectMany(contributor => contributor.NavigationItems.Select(item => new { contributor.PluginId, Item = item }))
                .Where(entry => string.IsNullOrWhiteSpace(entry.Item.RequiredPermission) ||
                                EndpointAuthorizationExtensions.UserHasPermission(httpContext.User, entry.Item.RequiredPermission))
                .OrderBy(entry => entry.Item.Order)
                .ThenBy(entry => entry.Item.Label, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new PluginAdminNavigationApiResponse(
                    entry.PluginId,
                    entry.Item.Id,
                    entry.Item.Label,
                    entry.Item.To,
                    entry.Item.Icon,
                    entry.Item.Order))
                .ToArray();

            return Results.Ok(items);
        }).WithName("PluginExtensions_AdminNavigation");

        group.MapGet("/ui-chain", HandleUiChainAsync)
            .WithName("PluginExtensions_AdminUiChain");

        group.MapGet("/surface-ui-chain", HandleSurfaceUiChainCatalogAsync)
            .WithName("PluginExtensions_AdminSurfaceUiChainCatalog");

        group.MapMethods("/plugins/{pluginId}/{**routePath}", ["GET", "POST", "PUT", "DELETE"], HandlePluginAdminRouteAsync)
            .WithName("PluginExtensions_AdminApiProxy");
    }

    /// <summary>
    /// The ordered plugin ids whose admin UI bundles the shell may load for the caller's
    /// effective workspace.
    /// <para>
    /// The shell previously loaded every admin bundle the manifest carried, which made the
    /// workspace plugin assignment ineffective on the UI layer: an unassigned plugin's
    /// interface still appeared. The chain folds assignment, entitlement, capability and
    /// runtime health together, so the decision stays on the server.
    /// </para>
    /// </summary>
    private static async Task<IResult> HandleUiChainAsync(
        HttpContext httpContext,
        IWorkspaceScopeContext workspaceScope,
        WorkspaceUiChainResolver chainResolver,
        CancellationToken cancellationToken)
    {
        // Same resolution as the proxy route (#109): a bound session keeps its own workspace,
        // and only a platform operator may name one through ?workspaceKey=.
        var workspaceKey = PluginAdminWorkspaceResolver.Resolve(httpContext, workspaceScope.WorkspaceKey);
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            // Kein Workspace ausgewählt heißt: keine Plugin-Oberfläche gilt. Das ist eine
            // Antwort, keine fehlerhafte Anfrage.
            //
            // Vorher stand hier 400. Auf einer FRISCHEN Installation gibt es noch gar keinen
            // Workspace, die Shell fragt beim ersten Laden also zwangsläufig ohne — und der
            // Betreiber sah einen roten Konsolenfehler, bevor er überhaupt etwas anlegen
            // konnte. Ein Fehler im Normalfall erzieht dazu, Fehler zu übersehen.
            //
            // Die Shell verhält sich in beiden Fällen gleich (sie rendert ohne Plugin-UI);
            // was sich ändert, ist die Aussage: leere Kette statt "deine Anfrage war falsch".
            return Results.Ok(new UiChainApiResponse(string.Empty, []));
        }

        var chain = await chainResolver
            .ResolveAsync(workspaceKey, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new UiChainApiResponse(workspaceKey, chain));
    }

    /// <summary>
    /// Die Flächen-Ladekette für einen EDITOR: was auf dieser Fläche eingebaut werden könnte.
    /// <para>
    /// Der Composer lud seine Flächen-Bundles bisher über die öffentliche Render-Kette, und die
    /// ist auf das gekürzt, was das veröffentlichte Layout verlangt. Für einen Editor ist das
    /// zirkulär: die Block-Palette braucht das Bundle, um dessen Blöcke anzubieten, und das
    /// Bundle kam erst, wenn einer seiner Blöcke bereits im Layout stand. Eine leere Fläche
    /// blieb deshalb dauerhaft leer — ohne Fehler, ohne Hinweis.
    /// </para>
    /// <para>
    /// Bewusst hier und nicht als Parameter am anonymen Endpunkt: Die ungekürzte Kette ist das
    /// Plugin-Inventar einer Fläche. <c>/workspace/public/ui-chain</c> antwortet einem
    /// nicht angemeldeten Aufrufer absichtlich mit 404 statt einer aufzählbaren Liste; ein
    /// Schalter, der diese Kürzung dort abschaltet, wäre genau die Lücke, die dort vermieden
    /// wird. Diese Gruppe verlangt eine Anmeldung, der Editor läuft ohnehin darin.
    /// </para>
    /// </summary>
    private static async Task<IResult> HandleSurfaceUiChainCatalogAsync(
        HttpContext httpContext,
        string? surfaceKey,
        IWorkspaceScopeContext workspaceScope,
        WorkspaceUiChainResolver chainResolver,
        CancellationToken cancellationToken)
    {
        var workspaceKey = PluginAdminWorkspaceResolver.Resolve(httpContext, workspaceScope.WorkspaceKey);
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            // Wie bei /ui-chain: kein Workspace ist eine Antwort, keine fehlerhafte Anfrage.
            return Results.Ok(new UiChainApiResponse(string.Empty, []));
        }

        var chain = await chainResolver
            .ResolveAsync(workspaceKey, surfaceKey, WorkspaceUiChainPurpose.Catalog, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new UiChainApiResponse(workspaceKey, chain));
    }

    private static async Task<IResult> HandlePluginAdminRouteAsync(
        string pluginId,
        string? routePath,
        HttpContext httpContext,
        ICalloraPluginCatalog pluginCatalog,
        IWorkspaceScopeContext workspaceScope,
        // Optional: Ein Host ohne Fehlerbudget rechnet nichts zu und verhält sich unverändert.
        [FromServices] PluginFaultRegistry? faults,
        CancellationToken cancellationToken)
    {
        var contributors = pluginCatalog.GetExports<IHostAdminApiExtensionContributor>();
        var match = PluginAdminRouteMatcher.FindMatch(
            contributors,
            pluginId,
            httpContext.Request.Method,
            routePath ?? string.Empty);

        if (match is null)
        {
            return ApiProblems.NotFound($"No plugin admin route found for '{pluginId}/{routePath}'.");
        }

        if (!EndpointAuthorizationExtensions.UserHasPermission(httpContext.User, match.Route.RequiredPermission))
        {
            return Results.Forbid();
        }

        // The effective workspace — the caller's bound one, or the one a platform
        // operator selected via ?workspaceKey=. Resolving it here, before the
        // availability gate, is the point of #109: a query-selected workspace used
        // to reach the plugin ungated because only the token-bound value was read.
        var workspaceKey = PluginAdminWorkspaceResolver.Resolve(httpContext, workspaceScope.WorkspaceKey);

        if (match.Route.Scope == HostAdminApiRouteScope.Workspace)
        {
            if (string.IsNullOrWhiteSpace(workspaceKey))
            {
                return ApiProblems.BadRequest(
                    "A workspace is required. Platform operators select one with ?workspaceKey=.");
            }

            // A permitted caller still only reaches the plugin when it is effectively
            // available in that workspace (REV2 §13): an entitlement lapse, missing
            // capability, unhealthy runtime or inactive workspace returns 403 rather
            // than routing into a plugin that should be dark. Ordered after the
            // permission check so unavailability is never disclosed to callers who
            // lack the permission.
            if (httpContext.RequestServices.GetService<IPluginAvailabilityEvaluator>() is { } availabilityEvaluator)
            {
                var availability = await availabilityEvaluator
                    .EvaluateAsync(match.Contributor.PluginId, workspaceKey, cancellationToken)
                    .ConfigureAwait(false);
                if (!availability.IsAvailable)
                {
                    // Generic response — no internal availability detail is leaked.
                    return Results.Forbid();
                }
            }
        }

        var request = new HostAdminApiRequest(
            match.Contributor.PluginId,
            httpContext.Request.Method,
            routePath ?? string.Empty,
            match.RouteValues,
            HttpQueryValues.Read(httpContext.Request.Query),
            await ReadJsonBodyAsync(httpContext, cancellationToken).ConfigureAwait(false),
            ResolveUserId(httpContext.User),
            workspaceKey);

        HostAdminApiResponse response;
        try
        {
            response = await match.Route.Handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Der Aufrufer hat abgebrochen. Das ist keine Verfehlung des Plugins.
            throw;
        }
        catch (Exception)
        {
            // Zurechnen und weiterwerfen: Die Antwort bleibt, was sie war — der zentrale
            // CalloraExceptionHandler macht daraus ein RFC-9457-Problem. Hier wird nur
            // festgehalten, WER geworfen hat, denn genau das ging bisher verloren: Ein
            // Plugin, das ausschließlich über Admin-Routen scheitert, lief unbemerkt weiter,
            // während dieselbe Fehlerrate auf einer Surface-Route längst gezählt wurde.
            faults?.Record(match.Contributor.PluginId, PluginFaultOrigin.HttpRoute);
            throw;
        }

        var statusCode = response.StatusCode;

        if (response.Payload is null)
        {
            return Results.StatusCode(statusCode);
        }

        return Results.Json(response.Payload, statusCode: statusCode);
    }

    private static async Task<JsonElement?> ReadJsonBodyAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (context.Request.ContentLength is null or <= 0)
        {
            return null;
        }

        if (context.Request.Body is null)
        {
            return null;
        }

        try
        {
            using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: cancellationToken).ConfigureAwait(false);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ResolveUserId(ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user.FindFirstValue("sub")
               ?? user.Identity?.Name;
    }
}
