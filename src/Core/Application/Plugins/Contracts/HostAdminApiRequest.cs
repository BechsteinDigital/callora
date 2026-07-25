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
/// The caller's bound workspace, resolved from the authenticated principal. Non-null for a
/// workspace-scoped operator (who may only act within it); null for a platform operator
/// (super-admin/global), who is not bound to a single workspace. Handlers of workspace-scoped
/// resources must use this value as the authoritative scope, never a client-supplied workspace.
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
