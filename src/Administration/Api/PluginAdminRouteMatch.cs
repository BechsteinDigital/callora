using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Administration.Api;

public sealed record PluginAdminRouteMatch(
    IHostAdminApiExtensionContributor Contributor,
    HostAdminApiRouteRegistration Route,
    IReadOnlyDictionary<string, string> RouteValues);
