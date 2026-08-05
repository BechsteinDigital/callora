using Callora.Core.Application.Surfaces;

namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Pre-accept context handed to an <see cref="IWebSocketConnectAuthorizer"/>. It
/// exposes everything an authorizer needs to validate a connect-token without
/// leaking the underlying HTTP request: the resolved route values, the query
/// string (the usual carrier for a connect-token, since browsers cannot set
/// arbitrary handshake headers) and the requested sub-protocols.
/// </summary>
/// <param name="PluginId">Plugin that owns the matched route.</param>
/// <param name="RoutePath">Route path relative to the plugin root (for example: media-streams/abc).</param>
/// <param name="RouteValues">Values extracted from the route template (for example: sessionId → abc).</param>
/// <param name="Query">Query-string values of the connect request (case-insensitive keys).</param>
/// <param name="RequestedSubProtocols">Sub-protocols the client offered during the handshake.</param>
/// <param name="Caller">
/// The surface caller the upgrade carries, when the request presented a usable
/// surface context (ADR-017 §9). Null for a connect that carries none, which is the
/// normal case for an out-of-process agent using a connect-token. The host attaches
/// it only when the handshake's <c>Origin</c> matches the requested host, because a
/// browser sends cookies on a cross-site upgrade and no same-origin policy stops it.
/// </param>
public sealed record HostWebSocketConnectRequest(
    string PluginId,
    string RoutePath,
    IReadOnlyDictionary<string, string> RouteValues,
    IReadOnlyDictionary<string, string[]> Query,
    IReadOnlyList<string> RequestedSubProtocols,
    SurfaceCaller? Caller = null);
