using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Api;

public sealed record PluginAdminRouteMatch(
    IHostAdminApiExtensionContributor Contributor,
    HostAdminApiRouteRegistration Route,
    IReadOnlyDictionary<string, string> RouteValues);
