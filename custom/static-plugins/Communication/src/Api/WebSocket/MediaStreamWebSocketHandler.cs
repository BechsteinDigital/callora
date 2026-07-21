using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Application.Streaming.Protocol;
using Callora.Plugin.Communication.Infrastructure.Transport;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// Services an accepted media WebSocket: resolves the (already-authorized) session from the
/// connection subject, opens the call's audio stream and runs the <see cref="MediaBridge"/>. When
/// no live call exists yet (B5) it emits a <c>stop</c> and closes cleanly. The session is marked
/// <see cref="MediaStreamSessionStatus.Closed"/> when the socket ends.
/// </summary>
public sealed class MediaStreamWebSocketHandler(
    IMediaStreamSessionStore sessionStore,
    ICallAudioStreamProvider audioStreamProvider) : IHostWebSocketHandler
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

            await using var scopedAudio = audioStream;
            using var channel = new WebSocketMediaFrameChannel(connection.Socket);
            var start = new MediaStreamStartMetadata(
                session.Id,
                session.CallId,
                EncodingLabel(session.Format.Codec),
                session.Format.SampleRateHz);

            await new MediaBridge(scopedAudio, channel).RunAsync(start, cancellationToken).ConfigureAwait(false);
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
