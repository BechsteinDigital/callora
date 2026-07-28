using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Provides public (anonymous) HTTP endpoints hosted under the platform's reserved
/// <c>/public/{pluginId}/…</c> prefix. This is the public-facing counterpart to
/// <see cref="IHostWebSocketEndpointContributor"/> and
/// <see cref="IHostAdminApiExtensionContributor"/>: where the Admin API is
/// operator-authenticated request/response JSON, these routes are fully public —
/// <strong>the host enforces no authentication at the cookie/JWT layer.</strong>
/// The handler is solely responsible for input validation, token verification,
/// and any access control appropriate for its use case (for example: a signed
/// invitation token or a public webhook callback).
/// </summary>
/// <remarks>
/// Typical use cases include: publicly accessible HTML forms (e.g. meeting join
/// pages, survey pages), webhook ingestion endpoints (e.g. carrier SMS callbacks),
/// and one-time redirect links. Because these routes are anonymous at the
/// platform level, contributors must never expose sensitive data without
/// performing their own verification inside the handler.
/// </remarks>
[CalloraExtensible("Extension point — implement to contribute plugin public HTTP endpoints (anonymous, host-level public surface)")]
public interface IHostPublicHttpEndpointContributor
{
    /// <summary>
    /// Stable plugin identifier owning these endpoints. Forms the first segment
    /// of the public route (<c>/public/{PluginId}/…</c>).
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// Declared public HTTP routes handled by the plugin.
    /// </summary>
    IReadOnlyList<HostPublicHttpRouteRegistration> PublicHttpRoutes { get; }
}
