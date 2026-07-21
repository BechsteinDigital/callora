using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Streaming;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// Contributes the plugin's media WebSocket endpoint under the host's reserved prefix:
/// <c>/ws/communication/media/{connectToken}</c>. The connect token is validated (and consumed)
/// by <see cref="MediaStreamConnectTokenAuthorizer"/> before the socket is accepted;
/// <see cref="MediaStreamWebSocketHandler"/> then bridges call audio to the consumer.
/// </summary>
public sealed class CommunicationMediaWebSocketContributor : IHostWebSocketEndpointContributor
{
    /// <summary>Route template relative to the plugin prefix (<c>/ws/communication/…</c>).</summary>
    public const string RouteTemplate = "media/{connectToken}";

    private readonly IReadOnlyList<HostWebSocketRouteRegistration> _routes;

    /// <summary>Wires the media route with its connect-token authorizer and bridge handler.</summary>
    public CommunicationMediaWebSocketContributor(
        IMediaStreamSessionStore sessionStore,
        ICallAudioStreamProvider audioStreamProvider)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(audioStreamProvider);

        _routes =
        [
            new HostWebSocketRouteRegistration(
                RouteTemplate,
                new MediaStreamConnectTokenAuthorizer(sessionStore),
                new MediaStreamWebSocketHandler(sessionStore, audioStreamProvider))
        ];
    }

    /// <inheritdoc />
    public string PluginId => CommunicationPlugin.Id;

    /// <inheritdoc />
    public IReadOnlyList<HostWebSocketRouteRegistration> WebSocketRoutes => _routes;
}
