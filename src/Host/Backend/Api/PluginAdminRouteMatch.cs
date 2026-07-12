using Callora.Host.PluginContracts.Application.Plugins;

namespace Callora.Host.Backend.Api;

public sealed record PluginAdminRouteMatch(
    IHostAdminApiExtensionContributor Contributor,
    HostAdminApiRouteRegistration Route,
    IReadOnlyDictionary<string, string> RouteValues);
