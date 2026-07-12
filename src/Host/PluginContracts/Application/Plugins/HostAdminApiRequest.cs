using System.Text.Json;

namespace Callora.Host.PluginContracts.Application.Plugins;

/// <summary>
/// Request model passed to plugin-provided Admin API handlers.
/// </summary>
/// <param name="PluginId">Owning plugin identifier.</param>
/// <param name="HttpMethod">HTTP method.</param>
/// <param name="RoutePath">Route path relative to plugin root.</param>
/// <param name="RouteValues">Resolved route values from template parameters.</param>
/// <param name="Query">Query values (case-insensitive keys).</param>
/// <param name="Body">Parsed JSON body when provided.</param>
/// <param name="UserId">Caller user identifier when available.</param>
public sealed record HostAdminApiRequest(
    string PluginId,
    string HttpMethod,
    string RoutePath,
    IReadOnlyDictionary<string, string> RouteValues,
    IReadOnlyDictionary<string, string[]> Query,
    JsonElement? Body,
    string? UserId);
