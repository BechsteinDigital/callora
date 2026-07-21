using System.Threading.Channels;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Streaming.Pacing;
using Callora.Plugin.Communication.Application.Streaming.Protocol;

namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// Couples a call's duplex <see cref="ICallAudioStream"/> to a consumer's
/// <see cref="IMediaFrameChannel"/>, translating both directions of the Twilio-Media-Streams
/// protocol: inbound call audio → <c>media</c> frames, and <c>media</c> frames → outbound call
/// audio. The inbound frame handler must not block, so received frames are queued and forwarded
/// by a dedicated pump; the pumps run until either side ends, then all are torn down.
/// The call→consumer buffer is bounded (drop-oldest, real-time), and consumer→call audio is paced
/// through a <see cref="PacedAudioSender"/>; a <c>clear</c> frame flushes it for barge-in.
/// </summary>
public sealed class MediaBridge(ICallAudioStream audioStream, IMediaFrameChannel channel, IPacingClock? pacingClock = null)
{
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
        audioStream.FrameReceived += OnFrameReceived;
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
                    case MediaStreamEventType.Media when TryDecodePayload(message.Payload, out var frame):
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

    private static bool TryDecodePayload(string? base64, out byte[] frame)
    {
        if (!string.IsNullOrEmpty(base64))
        {
            try
            {
                frame = Convert.FromBase64String(base64);
                return true;
            }
            catch (FormatException)
            {
                // A misbehaving consumer must not kill the stream — drop the frame.
            }
        }

        frame = [];
        return false;
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
