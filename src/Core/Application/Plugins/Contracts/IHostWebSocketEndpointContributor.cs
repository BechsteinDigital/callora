using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Provides real-time WebSocket endpoints hosted under the platform's reserved
/// <c>/ws/{pluginId}/…</c> prefix. This is the duplex counterpart to
/// <see cref="IHostAdminApiExtensionContributor"/>: where the Admin API is
/// request/response JSON, these routes are long-lived bidirectional streams
/// (for example: Twilio-Media-Streams-style audio for out-of-process AI agents).
/// The host validates every connect through the route's
/// <see cref="IWebSocketConnectAuthorizer"/> before the socket is accepted.
/// </summary>
[CalloraExtensible("Extension point — implement to contribute plugin WebSocket endpoints (host-level real-time surface)")]
public interface IHostWebSocketEndpointContributor
{
    /// <summary>
    /// Stable plugin identifier owning these endpoints. Forms the first segment
    /// of the public route (<c>/ws/{PluginId}/…</c>).
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// Declared WebSocket routes handled by the plugin.
    /// </summary>
    IReadOnlyList<HostWebSocketRouteRegistration> WebSocketRoutes { get; }
}
