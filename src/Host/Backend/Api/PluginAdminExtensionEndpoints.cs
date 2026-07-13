using System.Security.Claims;
using System.Text.Json;
using Callora.Host.Backend.Infrastructure.Security;
using Callora.Hosting.Application.Plugins;
using Microsoft.Extensions.Primitives;
using Callora.Host.PluginContracts.Application.Plugins;

namespace Callora.Host.Backend.Api;

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

        group.MapMethods("/plugins/{pluginId}/{**routePath}", ["GET", "POST", "PUT", "DELETE"], HandlePluginAdminRouteAsync)
            .WithName("PluginExtensions_AdminApiProxy");
    }

    private static async Task<IResult> HandlePluginAdminRouteAsync(
        string pluginId,
        string? routePath,
        HttpContext httpContext,
        ICalloraPluginCatalog pluginCatalog,
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

        var request = new HostAdminApiRequest(
            match.Contributor.PluginId,
            httpContext.Request.Method,
            routePath ?? string.Empty,
            match.RouteValues,
            ReadQuery(httpContext.Request.Query),
            await ReadJsonBodyAsync(httpContext, cancellationToken).ConfigureAwait(false),
            ResolveUserId(httpContext.User));

        var response = await match.Route.Handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
        var statusCode = response.StatusCode;

        if (response.Payload is null)
        {
            return Results.StatusCode(statusCode);
        }

        return Results.Json(response.Payload, statusCode: statusCode);
    }

    private static IReadOnlyDictionary<string, string[]> ReadQuery(IQueryCollection query)
    {
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query)
        {
            result[pair.Key] = ToArray(pair.Value);
        }

        return result;
    }

    private static string[] ToArray(StringValues values)
    {
        if (values.Count == 0)
            return Array.Empty<string>();

        var result = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            result[i] = values[i] ?? string.Empty;
        }

        return result;
    }

    private static async Task<JsonElement?> ReadJsonBodyAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (context.Request.ContentLength is null or <= 0)
            return null;

        if (context.Request.Body is null)
            return null;

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
