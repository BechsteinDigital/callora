using Callora.Core.Application.Plugins.Contracts;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// Contributes the plugin's WebRTC signalling WebSocket endpoint under the host's reserved prefix:
/// <c>/ws/communication/webrtc/{connectToken}</c>. The connect token is validated (and consumed) by
/// <see cref="WebRtcSignalingConnectTokenAuthorizer"/> before the socket is accepted;
/// <see cref="WebRtcSignalingWebSocketHandler"/> then mediates SDP/ICE between the browser and a
/// server-side SDK peer. The sibling of <see cref="CommunicationMediaWebSocketContributor"/> on the same
/// host WebSocket seam.
/// </summary>
public sealed class WebRtcSignalingContributor : IHostWebSocketEndpointContributor
{
    /// <summary>Route template relative to the plugin prefix (<c>/ws/communication/…</c>).</summary>
    public const string RouteTemplate = "webrtc/{connectToken}";

    private readonly IReadOnlyList<HostWebSocketRouteRegistration> _routes;

    /// <summary>Wires the WebRTC signalling route with its connect-token authorizer and signalling handler.</summary>
    public WebRtcSignalingContributor(
        IWebRtcSignalingTokenStore tokenStore,
        IWebRtcSignalingSessionResolver sessionResolver,
        ILogger<WebRtcSignalingWebSocketHandler> handlerLogger)
    {
        ArgumentNullException.ThrowIfNull(tokenStore);
        ArgumentNullException.ThrowIfNull(sessionResolver);
        ArgumentNullException.ThrowIfNull(handlerLogger);

        _routes =
        [
            new HostWebSocketRouteRegistration(
                RouteTemplate,
                new WebRtcSignalingConnectTokenAuthorizer(tokenStore),
                new WebRtcSignalingWebSocketHandler(sessionResolver, handlerLogger))
        ];
    }

    /// <inheritdoc />
    public string PluginId => CommunicationPlugin.Id;

    /// <inheritdoc />
    public IReadOnlyList<HostWebSocketRouteRegistration> WebSocketRoutes => _routes;
}
