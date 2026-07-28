using Callora.Core.Application.Plugins.Contracts;
using CalloraVoipSdk.WebRtc;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// Services an accepted WebRTC signalling WebSocket, mediating SDP/ICE between the browser and a
/// server-side SDK <see cref="IPeerConnection"/>. Callora is the offerer (matching the SDK browser-interop
/// flow): it creates the peer, sends the <c>offer</c> plus trickled local ICE candidates, applies the
/// browser's <c>answer</c> and then starts the transport, and applies trickled remote candidates. When the
/// peer reaches <see cref="PeerConnectionState.Connected"/> it attaches the peer to the resolved
/// <see cref="WebRtcSignalingSession.Channel"/> exactly once (raising its incoming call), then keeps the
/// socket open for further trickle until close. No media is routed here — v1 establishes peer + call
/// control only. Malformed frames are logged and ignored; the peer is disposed on close/failure unless a
/// call has taken it over.
/// </summary>
public sealed class WebRtcSignalingWebSocketHandler(
    IWebRtcSignalingSessionResolver sessionResolver,
    ILogger<WebRtcSignalingWebSocketHandler> logger) : IHostWebSocketHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(HostWebSocketConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var session = await sessionResolver.ResolveAsync(connection.Subject, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            // The channel was deprovisioned between accept and handling — nothing to signal, close cleanly.
            logger.LogWarning("WebRTC signalling: no session resolved for subject; closing.");
            return;
        }

        using var channel = new WebRtcSignalingChannel(connection.Socket);
        var peer = session.Client.CreatePeer();
        var negotiation = new WebRtcSignalingNegotiation(peer, session, channel, logger);

        try
        {
            await negotiation.StartAsync(cancellationToken).ConfigureAwait(false);
            await RunSignalingLoopAsync(channel, negotiation, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await negotiation.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task RunSignalingLoopAsync(
        WebRtcSignalingChannel channel,
        WebRtcSignalingNegotiation negotiation,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var text = await channel.ReceiveTextAsync(cancellationToken).ConfigureAwait(false);
            if (text is null)
            {
                // Socket closed by the client.
                return;
            }

            var message = WebRtcSignalMessage.TryParse(text);
            if (message is null)
            {
                // Malformed or non-object frame — never surface internals to the client; log and skip.
                logger.LogWarning("WebRTC signalling: ignored a malformed frame.");
                continue;
            }

            await negotiation.HandleAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }
}
