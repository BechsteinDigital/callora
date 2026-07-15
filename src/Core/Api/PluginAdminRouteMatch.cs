using Callora.Host.PluginContracts.Application.Plugins;

namespace Callora.Core.Api;

public sealed record PluginAdminRouteMatch(
    IHostAdminApiExtensionContributor Contributor,
    HostAdminApiRouteRegistration Route,
    IReadOnlyDictionary<string, string> RouteValues);
