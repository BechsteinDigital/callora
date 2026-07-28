namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Request model passed to plugin-provided public HTTP route handlers.
/// All fields are pre-parsed by the host; the handler never receives an
/// ASP.NET <c>HttpContext</c> directly.
/// </summary>
/// <param name="PluginId">Owning plugin identifier.</param>
/// <param name="Method">HTTP method of the incoming request (for example: <c>GET</c>, <c>POST</c>).</param>
/// <param name="RoutePath">Route path relative to the plugin prefix (for example: <c>join/abc123</c>).</param>
/// <param name="RouteValues">
/// Resolved route values extracted from template parameters (case-insensitive keys).
/// For example, a template <c>join/{invitationToken}</c> matched against
/// <c>join/abc123</c> yields <c>{ "invitationToken": "abc123" }</c>.
/// </param>
/// <param name="Query">
/// Query string key-value pairs (case-insensitive keys, single string values).
/// When a query key appears multiple times, the first value is used.
/// </param>
/// <param name="Headers">
/// Selected request headers (case-insensitive keys). The host forwards only a
/// curated allowlist of request headers (for example: <c>Content-Type</c>,
/// <c>Accept</c>, <c>User-Agent</c>); sensitive headers such as <c>Cookie</c> and
/// <c>Authorization</c> are never forwarded on this anonymous public endpoint.
/// Handlers must not assume all request headers are present.
/// </param>
/// <param name="Body">
/// Raw request body string, or <c>null</c> when the request carries no body
/// (for example: a GET request). The host reads the body up to a 1 MB limit;
/// requests exceeding this limit are rejected with HTTP 413 before the handler
/// is invoked.
/// </param>
public sealed record HostPublicHttpRequest(
    string PluginId,
    string Method,
    string RoutePath,
    IReadOnlyDictionary<string, string?> RouteValues,
    IReadOnlyDictionary<string, string> Query,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);
