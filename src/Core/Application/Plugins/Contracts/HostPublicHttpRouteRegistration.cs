namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// One plugin-provided public HTTP route declaration.
/// </summary>
/// <remarks>
/// There is no separate authorizer on public routes — the host enforces no
/// authentication before invoking the handler. The <see cref="Handler"/> is
/// fully responsible for token verification and input validation, as befits a
/// public surface (for example: verifying a signed invitation token, validating
/// a webhook HMAC, or serving a static page).
/// </remarks>
/// <param name="Method">
/// HTTP method accepted by this route (for example: <c>GET</c>, <c>POST</c>).
/// Matching is case-insensitive.
/// </param>
/// <param name="RouteTemplate">
/// Route template relative to the plugin prefix
/// (for example: <c>join/{invitationToken}</c>).
/// Supports <c>{param}</c> segments; extracted values are surfaced in
/// <see cref="HostPublicHttpRequest.RouteValues"/>.
/// </param>
/// <param name="Handler">
/// Handler that processes the matched request and returns a
/// <see cref="HostPublicHttpResponse"/>. The handler runs in the context of
/// the platform's dependency-injection scope; unhandled exceptions are caught
/// by the host, which returns 500 without leaking details to the caller.
/// </param>
public sealed record HostPublicHttpRouteRegistration(
    string Method,
    string RouteTemplate,
    IHostPublicHttpRouteHandler Handler);
