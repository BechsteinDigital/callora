using System.Text.Json;

namespace Callora.Core.Application.Plugins.Contracts;

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
/// <param name="WorkspaceKey">
/// The effective workspace, resolved by the host: the caller's bound workspace when the
/// principal carries one — a client-supplied value can never override it — otherwise the
/// workspace a platform operator selected explicitly via <c>?workspaceKey=</c>. For a route
/// declared <see cref="HostAdminApiRouteScope.Workspace"/> this is non-null and the host has
/// already confirmed the plugin is available there; only a
/// <see cref="HostAdminApiRouteScope.Global"/> route may see null. Handlers must use this
/// value as the authoritative scope and never re-read a workspace from the query.
/// </param>
public sealed record HostAdminApiRequest(
    string PluginId,
    string HttpMethod,
    string RoutePath,
    IReadOnlyDictionary<string, string> RouteValues,
    IReadOnlyDictionary<string, string[]> Query,
    JsonElement? Body,
    string? UserId,
    string? WorkspaceKey = null);
