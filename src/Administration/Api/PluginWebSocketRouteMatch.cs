using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Administration.Api;

/// <summary>A resolved plugin WebSocket route with its extracted route values.</summary>
public sealed record PluginWebSocketRouteMatch(
    IHostWebSocketEndpointContributor Contributor,
    HostWebSocketRouteRegistration Route,
    IReadOnlyDictionary<string, string> RouteValues);
