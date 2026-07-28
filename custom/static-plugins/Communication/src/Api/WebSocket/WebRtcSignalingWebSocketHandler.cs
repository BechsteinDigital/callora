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
/// <remarks>
/// An answer deadline (<see cref="DefaultAnswerDeadline"/>, default 30 s) limits how long the handler will wait
/// for a browser answer after sending the offer. If no answer arrives within that window the connection is
/// closed cleanly (peer disposed, no error surfaced). Once an answer is received the deadline is disarmed
/// and the socket stays open indefinitely for continued trickle until the call ends or the host cancels.
/// </remarks>
public sealed class WebRtcSignalingWebSocketHandler : IHostWebSocketHandler
{
    // Default window from offer to answer. Browsers that fail to answer within this period are
    // considered stale/stuck; the connection is torn down cleanly (the peer is disposed in the finally).
    private static readonly TimeSpan DefaultAnswerDeadline = TimeSpan.FromSeconds(30);

    private readonly IWebRtcSignalingSessionResolver _sessionResolver;
    private readonly ILogger<WebRtcSignalingWebSocketHandler> _logger;
    private readonly TimeSpan _answerDeadline;

    /// <summary>
    /// Initialises the handler.
    /// </summary>
    /// <param name="sessionResolver">Resolves a minted token to its signalling session.</param>
    /// <param name="logger">Diagnostic logger.</param>
    /// <param name="answerDeadline">
    /// How long to wait for a browser answer after sending the offer. Defaults to
    /// <see cref="DefaultAnswerDeadline"/> (30 s). Supply a shorter value in tests to avoid real timers.
    /// </param>
    public WebRtcSignalingWebSocketHandler(
        IWebRtcSignalingSessionResolver sessionResolver,
        ILogger<WebRtcSignalingWebSocketHandler> logger,
        TimeSpan? answerDeadline = null)
    {
        _sessionResolver = sessionResolver;
        _logger = logger;
        _answerDeadline = answerDeadline ?? DefaultAnswerDeadline;
    }

    /// <inheritdoc />
    public async Task HandleAsync(HostWebSocketConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var session = await _sessionResolver.ResolveAsync(connection.Subject, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            // The channel was deprovisioned between accept and handling — nothing to signal, close cleanly.
            _logger.LogWarning("WebRTC signalling: no session resolved for subject; closing.");
            return;
        }

        // The deadline CTS is linked to the host token so either a host cancellation or a deadline
        // expiry terminates the loop. Once a valid answer arrives, the negotiation disarms the deadline
        // (CancelAfter(Infinite)) so the socket stays open for post-answer trickle.
        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineCts.CancelAfter(_answerDeadline);

        using var channel = new WebRtcSignalingChannel(connection.Socket);
        var peer = session.Client.CreatePeer();
        var negotiation = new WebRtcSignalingNegotiation(peer, session, channel, _logger);

        try
        {
            await negotiation.StartAsync(deadlineCts.Token).ConfigureAwait(false);
            await RunSignalingLoopAsync(channel, negotiation, deadlineCts).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Either the host cancelled or the answer deadline expired — both are expected close paths.
            // The peer is disposed in the finally block below; no error to surface.
        }
        finally
        {
            await negotiation.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task RunSignalingLoopAsync(
        WebRtcSignalingChannel channel,
        WebRtcSignalingNegotiation negotiation,
        CancellationTokenSource deadlineCts)
    {
        // Disarms the answer deadline once a valid answer is received, keeping the socket open for
        // continued post-answer trickle. CancelAfter(Infinite) is the idiomatic way to reset a CTS timer.
        void DisarmDeadline() => deadlineCts.CancelAfter(Timeout.InfiniteTimeSpan);

        while (!deadlineCts.Token.IsCancellationRequested)
        {
            var text = await channel.ReceiveTextAsync(deadlineCts.Token).ConfigureAwait(false);
            if (text is null)
            {
                // Socket closed by the client.
                return;
            }

            var message = WebRtcSignalMessage.TryParse(text);
            if (message is null)
            {
                // Malformed or non-object frame — never surface internals to the client; log and skip.
                _logger.LogWarning("WebRTC signalling: ignored a malformed frame.");
                continue;
            }

            await negotiation.HandleAsync(message, deadlineCts.Token, DisarmDeadline).ConfigureAwait(false);
        }
    }
}
