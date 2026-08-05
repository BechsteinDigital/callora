using System.Threading.Channels;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Streaming.Pacing;
using Callora.Plugin.Communication.Application.Streaming.Protocol;
using Callora.Plugin.Communication.Domain.Streaming;

namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// Couples a call's duplex <see cref="ICallAudioStream"/> to a consumer's
/// <see cref="IMediaFrameChannel"/>, translating both directions of the Twilio-Media-Streams
/// protocol: inbound call audio → <c>media</c> frames, and <c>media</c> frames → outbound call
/// audio. The inbound frame handler must not block, so received frames are queued and forwarded
/// by a dedicated pump; the pumps run until either side ends, then all are torn down.
/// The call→consumer buffer is bounded (drop-oldest, real-time), and consumer→call audio is paced
/// through a <see cref="PacedAudioSender"/>; a <c>clear</c> frame flushes it for barge-in.
/// <para>
/// The session's <see cref="MediaStreamDirection"/> is enforced here rather than merely recorded
/// (#114). It is the only place both audio paths are visible, so a listen-only consumer's frames
/// are dropped and a speak-only consumer is never sent call audio — regardless of what it asks for
/// once the socket is open. Control frames (<c>mark</c>, <c>clear</c>, <c>stop</c>) stay available
/// in every direction; they carry no audio.
/// </para>
/// </summary>
public sealed class MediaBridge(
    ICallAudioStream audioStream,
    IMediaFrameChannel channel,
    MediaStreamDirection direction = MediaStreamDirection.Bidirectional,
    IPacingClock? pacingClock = null)
{
    // Relative to the consumer: Inbound means it listens, Outbound means it speaks.
    private readonly bool _consumerMayListen = direction is MediaStreamDirection.Inbound or MediaStreamDirection.Bidirectional;
    private readonly bool _consumerMaySpeak = direction is MediaStreamDirection.Outbound or MediaStreamDirection.Bidirectional;

    // Call → consumer buffer: bounded and drop-oldest so a slow consumer cannot grow it and stale
    // real-time frames are dropped rather than delaying fresh audio (~4 s at 20 ms frames).
    private const int InboundQueueCapacity = 200;

    /// <summary>Runs the bridge until the consumer or the call side ends the stream.</summary>
    public async Task RunAsync(MediaStreamStartMetadata start, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linked.Token;

        // Consumer → call is paced to a steady frame cadence; a caller-supplied clock keeps tests
        // deterministic, otherwise a monotone timer derived from the negotiated frame length.
        var ownsClock = pacingClock is null;
        var clock = pacingClock ?? new PeriodicPacingClock(TimeSpan.FromMilliseconds(audioStream.Format.FrameMilliseconds));
        var pacer = new PacedAudioSender(audioStream.SendAsync, clock);

        var inbound = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(InboundQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        void OnFrameReceived(object? sender, AudioFrameReceivedEventArgs e)
        {
            // Contract: the inbound handler must not block — copy the frame and enqueue (drop-oldest).
            inbound.Writer.TryWrite(e.Frame.ToArray());
        }

        await channel.SendAsync(MediaStreamMessage.ForStart(start), token).ConfigureAwait(false);

        // A speak-only consumer never subscribes to call audio in the first place, so no frame is
        // copied or queued for a socket that is not allowed to receive it.
        if (_consumerMayListen)
        {
            audioStream.FrameReceived += OnFrameReceived;
        }

        try
        {
            var callToConsumer = PumpCallToConsumerAsync(inbound.Reader, token);
            var consumerToCall = PumpConsumerToCallAsync(pacer, token);
            var pacedOutbound = pacer.RunAsync(token);

            await Task.WhenAny(callToConsumer, consumerToCall).ConfigureAwait(false);
            linked.Cancel();

            // Observe all so a genuine fault (not cancellation) surfaces to the handler.
            await ObserveAsync(callToConsumer).ConfigureAwait(false);
            await ObserveAsync(consumerToCall).ConfigureAwait(false);
            await ObserveAsync(pacedOutbound).ConfigureAwait(false);
        }
        finally
        {
            audioStream.FrameReceived -= OnFrameReceived;
            inbound.Writer.TryComplete();
            if (ownsClock && clock is IDisposable disposableClock)
            {
                disposableClock.Dispose();
            }
        }
    }

    private async Task PumpCallToConsumerAsync(ChannelReader<byte[]> reader, CancellationToken token)
    {
        try
        {
            await foreach (var frame in reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                await channel.SendAsync(MediaStreamMessage.Media(Convert.ToBase64String(frame)), token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal teardown.
        }
    }

    private async Task PumpConsumerToCallAsync(PacedAudioSender pacer, CancellationToken token)
    {
        try
        {
            while (true)
            {
                var message = await channel.ReceiveAsync(token).ConfigureAwait(false);
                if (message is null)
                {
                    return; // Consumer closed the channel.
                }

                switch (message.Event)
                {
                    // A listen-only consumer's audio is dropped, not forwarded and not fatal: the
                    // socket stays open for its control frames, but nothing it says reaches the call.
                    case MediaStreamEventType.Media when _consumerMaySpeak && TryDecodePayload(
                        message.Payload,
                        audioStream.Format.BytesPerFrame,
                        out var frame):
                        // Buffer for paced emission rather than sending straight through.
                        pacer.Enqueue(frame);
                        break;

                    case MediaStreamEventType.Mark:
                        // Echo the marker back (Twilio semantics: fired once "reached").
                        await channel.SendAsync(message, token).ConfigureAwait(false);
                        break;

                    case MediaStreamEventType.Clear:
                        // Barge-in: drop the agent's queued playback immediately.
                        pacer.Flush();
                        break;

                    case MediaStreamEventType.Stop:
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal teardown.
        }
    }

    /// <summary>
    /// Decodes one base64 audio payload, enforcing the negotiated frame size (#108).
    /// The encoded length is checked <em>before</em> decoding, so an oversized payload
    /// never gets allocated; the decoded frame must then match the format exactly,
    /// because a stream that agreed on 20 ms µ-law has no reason to send anything else.
    /// A violating frame is dropped, not fatal — a misbehaving consumer must not kill
    /// the call.
    /// </summary>
    private static bool TryDecodePayload(string? base64, int expectedFrameBytes, out byte[] frame)
    {
        frame = [];
        if (string.IsNullOrEmpty(base64))
        {
            return false;
        }

        // 4 base64 chars per 3 bytes; reject before allocating.
        if ((long)base64.Length / 4 * 3 > CommunicationStreamLimits.MaxAudioFrameBytes)
        {
            return false;
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return false;
        }

        if (decoded.Length != expectedFrameBytes || decoded.Length > CommunicationStreamLimits.MaxAudioFrameBytes)
        {
            return false;
        }

        frame = decoded;
        return true;
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the expected stop signal; genuine faults still propagate.
        }
    }
}
