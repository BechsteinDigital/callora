using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Application.Streaming.Protocol;
using Callora.Plugin.Communication.Infrastructure.Transport;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// Services an accepted media WebSocket: resolves the (already-authorized) session from the
/// connection subject, opens the call's audio stream and runs the <see cref="MediaBridge"/> under the
/// session's direction. When no live call exists yet (B5) it emits a <c>stop</c> and closes cleanly.
/// The session is marked <see cref="MediaStreamSessionStatus.Closed"/> when the socket ends.
/// <para>
/// While the bridge runs, the socket is registered against its call so ending the call aborts it
/// (#114). Without that registration a hang-up would close the audio path but leave the consumer's
/// socket open on a conversation that no longer exists.
/// </para>
/// </summary>
public sealed class MediaStreamWebSocketHandler(
    IMediaStreamSessionStore sessionStore,
    ICallAudioStreamProvider audioStreamProvider,
    MediaStreamConnectionRegistry connections) : IHostWebSocketHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(HostWebSocketConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (!TrySplitSubject(connection.Subject, out var workspaceKey, out var sessionId))
        {
            return;
        }

        var session = await sessionStore.GetAsync(workspaceKey, sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return;
        }

        try
        {
            var audioStream = await audioStreamProvider.OpenAsync(session.CallId, cancellationToken).ConfigureAwait(false);
            if (audioStream is null)
            {
                // No live call to attach to yet (B5) — signal end and close cleanly.
                using var idleChannel = new WebSocketMediaFrameChannel(connection.Socket);
                await idleChannel.SendAsync(MediaStreamMessage.Stop, cancellationToken).ConfigureAwait(false);
                return;
            }

            // The stream is call-scoped and owned by the SdkCallAudioRegistrar, which disposes it on
            // Call-Terminated.  The WS handler is a pure consumer: it opens and reads the stream but
            // must never dispose it — doing so while the call is still live would tear down the shared
            // audio path for the entire call.
            var scopedAudio = audioStream;
            using var channel = new WebSocketMediaFrameChannel(connection.Socket);
            var start = new MediaStreamStartMetadata(
                session.Id,
                session.CallId,
                EncodingLabel(session.Format.Codec),
                session.Format.SampleRateHz);

            // Linked so both stop the bridge: the request ending (the consumer disconnects) and the
            // call ending (the terminator aborts what it finds registered).
            using var abort = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var registration = connections.Register(session.CallId, session.Id, abort);

            await new MediaBridge(scopedAudio, channel, session.Direction)
                .RunAsync(start, abort.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The call ended or the consumer disconnected; the finally below records the close.
        }
        finally
        {
            // Mark closed even on abrupt disconnect — the request token is already cancelled then.
            session.Close(DateTimeOffset.UtcNow);
            await sessionStore.UpdateAsync(session, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static string EncodingLabel(AudioCodec codec) => codec switch
    {
        AudioCodec.G711Ulaw => "audio/x-mulaw",
        AudioCodec.G711Alaw => "audio/x-alaw",
        _ => "audio/x-mulaw"
    };

    private static bool TrySplitSubject(string? subject, out string workspaceKey, out string sessionId)
    {
        workspaceKey = string.Empty;
        sessionId = string.Empty;
        if (string.IsNullOrEmpty(subject))
        {
            return false;
        }

        var separator = subject.IndexOf('/');
        if (separator <= 0 || separator >= subject.Length - 1)
        {
            return false;
        }

        workspaceKey = subject[..separator];
        sessionId = subject[(separator + 1)..];
        return true;
    }
}
