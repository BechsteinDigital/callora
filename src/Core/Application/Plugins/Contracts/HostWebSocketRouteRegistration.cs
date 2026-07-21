namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// One plugin-provided WebSocket route declaration. Authorization is a per-route
/// concern (the host runs <paramref name="Authorizer"/> before accepting the
/// socket), so each route carries its own gate rather than relying on the shared
/// cookie/JWT pipeline — out-of-process consumers connect with a connect-token,
/// not a browser session.
/// </summary>
/// <param name="RouteTemplate">Route template relative to plugin root (for example: media-streams/{sessionId}).</param>
/// <param name="Authorizer">Validates the connect request before the socket is accepted.</param>
/// <param name="Handler">Handler that services the accepted socket.</param>
public sealed record HostWebSocketRouteRegistration(
    string RouteTemplate,
    IWebSocketConnectAuthorizer Authorizer,
    IHostWebSocketHandler Handler);
