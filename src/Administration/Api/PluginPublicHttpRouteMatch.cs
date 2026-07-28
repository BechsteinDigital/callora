using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Administration.Api;

/// <summary>A resolved plugin public HTTP route with its extracted route values.</summary>
public sealed record PluginPublicHttpRouteMatch(
    IHostPublicHttpEndpointContributor Contributor,
    HostPublicHttpRouteRegistration Route,
    IReadOnlyDictionary<string, string> RouteValues);
