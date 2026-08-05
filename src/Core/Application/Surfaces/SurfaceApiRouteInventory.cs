using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// What the surface API prefix actually serves, and what it refused to serve
/// (#125 block B). Building this separately from dispatch is what makes "reserved
/// routes and collisions are rejected" observable: a refused route is recorded with
/// its reason instead of quietly never matching.
/// </summary>
public sealed class SurfaceApiRouteInventory
{
    private SurfaceApiRouteInventory(
        IReadOnlyList<SurfaceApiMountedRoute> routes,
        IReadOnlyList<SurfaceApiRouteRejection> rejections)
    {
        Routes = routes;
        Rejections = rejections;
    }

    /// <summary>Routes the host serves, in declaration order per plugin.</summary>
    public IReadOnlyList<SurfaceApiMountedRoute> Routes { get; }

    /// <summary>Declared routes the host refused to mount, with the reason.</summary>
    public IReadOnlyList<SurfaceApiRouteRejection> Rejections { get; }

    /// <summary>Builds the inventory from the currently exported contributors.</summary>
    /// <param name="contributors">Contributors from the plugin catalog.</param>
    public static SurfaceApiRouteInventory Build(IEnumerable<IHostSurfaceApiContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        var routes = new List<SurfaceApiMountedRoute>();
        var rejections = new List<SurfaceApiRouteRejection>();

        foreach (var contributor in contributors)
        {
            var pluginId = contributor.PluginId?.Trim() ?? string.Empty;
            if (!SurfaceApiRouteRules.IsAllowedPluginId(pluginId))
            {
                foreach (var route in contributor.Routes ?? [])
                {
                    rejections.Add(new SurfaceApiRouteRejection(
                        pluginId,
                        route.HttpMethod,
                        route.RouteTemplate,
                        SurfaceApiRouteRejectionReason.ReservedPluginId));
                }

                continue;
            }

            AddRoutes(pluginId, contributor.Routes ?? [], routes, rejections);
        }

        return new SurfaceApiRouteInventory(routes, rejections);
    }

    private static void AddRoutes(
        string pluginId,
        IReadOnlyList<HostSurfaceApiRouteRegistration> declared,
        List<SurfaceApiMountedRoute> routes,
        List<SurfaceApiRouteRejection> rejections)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var route in declared)
        {
            if (route?.Handler is null || string.IsNullOrWhiteSpace(route.HttpMethod))
            {
                continue;
            }

            var template = route.RouteTemplate;
            if (!SurfaceApiRouteRules.IsAllowedTemplate(template))
            {
                rejections.Add(new SurfaceApiRouteRejection(
                    pluginId,
                    route.HttpMethod,
                    template ?? string.Empty,
                    SurfaceApiRouteRejectionReason.InvalidTemplate));
                continue;
            }

            // First declaration wins. Letting the later one shadow it would make the
            // served behaviour depend on export order.
            if (!seen.Add($"{route.HttpMethod.Trim()} {template.Trim('/')}"))
            {
                rejections.Add(new SurfaceApiRouteRejection(
                    pluginId,
                    route.HttpMethod,
                    template,
                    SurfaceApiRouteRejectionReason.DuplicateRoute));
                continue;
            }

            routes.Add(new SurfaceApiMountedRoute(pluginId, route));
        }
    }
}
