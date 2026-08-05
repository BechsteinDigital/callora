using System.Text.Json;
using Callora.Core.Application.Surfaces;

namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// One surface API request as the handling plugin sees it (#125 block B). The scope
/// is resolved by the host and authoritative: it comes from the visitor's own surface
/// session, never from anything the request could set.
/// </summary>
/// <param name="PluginId">Plugin owning the matched route.</param>
/// <param name="HttpMethod">HTTP method.</param>
/// <param name="RoutePath">Route path relative to the plugin's surface API root.</param>
/// <param name="RouteValues">Resolved route values from template parameters.</param>
/// <param name="Query">Query values (case-insensitive keys).</param>
/// <param name="Body">Parsed JSON body when provided.</param>
/// <param name="RequestId">
/// Correlates this execution with the host's audit entry and log lines. Safe to echo
/// back to the caller and to quote in a support ticket.
/// </param>
/// <param name="TenantKey">Tenant the calling surface belongs to.</param>
/// <param name="WorkspaceKey">Workspace the calling surface belongs to.</param>
/// <param name="SurfaceKey">Surface the request was made from.</param>
/// <param name="Caller">
/// Who is calling. Guest or authenticated, distinguished by type: reaching the
/// identity requires matching <see cref="AuthenticatedSurfaceCaller"/>, so a handler
/// cannot mistake the presence of a subject for authentication (ADR-017 §3).
/// </param>
public sealed record HostSurfaceApiRequest(
    string PluginId,
    string HttpMethod,
    string RoutePath,
    IReadOnlyDictionary<string, string> RouteValues,
    IReadOnlyDictionary<string, string[]> Query,
    JsonElement? Body,
    string RequestId,
    string TenantKey,
    string WorkspaceKey,
    string SurfaceKey,
    SurfaceCaller Caller);
