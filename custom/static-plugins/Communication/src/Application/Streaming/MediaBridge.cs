using System.Threading.Channels;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Streaming.Protocol;

namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// Couples a call's duplex <see cref="ICallAudioStream"/> to a consumer's
/// <see cref="IMediaFrameChannel"/>, translating both directions of the Twilio-Media-Streams
/// protocol: inbound call audio → <c>media</c> frames, and <c>media</c> frames → outbound call
/// audio. The inbound frame handler must not block, so received frames are queued and forwarded
/// by a dedicated pump; the two pumps run until either side ends, then both are torn down.
/// The inbound queue is unbounded — a fast call producer with a slow consumer could grow it;
/// precise outbound pacing, backpressure/bounding and real barge-in buffering are deferred (B4a-3).
/// </summary>
public sealed class MediaBridge(ICallAudioStream audioStream, IMediaFrameChannel channel)
{
    /// <summary>Runs the bridge until the consumer or the call side ends the stream.</summary>
    public async Task RunAsync(MediaStreamStartMetadata start, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linked.Token;

        var inbound = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        void OnFrameReceived(object? sender, AudioFrameReceivedEventArgs e)
        {
            // Contract: the inbound handler must not block — copy the frame and enqueue.
            inbound.Writer.TryWrite(e.Frame.ToArray());
        }

        await channel.SendAsync(MediaStreamMessage.ForStart(start), token).ConfigureAwait(false);
        audioStream.FrameReceived += OnFrameReceived;
        try
        {
            var callToConsumer = PumpCallToConsumerAsync(inbound.Reader, token);
            var consumerToCall = PumpConsumerToCallAsync(token);

            await Task.WhenAny(callToConsumer, consumerToCall).ConfigureAwait(false);
            linked.Cancel();

            // Observe both so a genuine fault (not cancellation) surfaces to the handler.
            await ObserveAsync(callToConsumer).ConfigureAwait(false);
            await ObserveAsync(consumerToCall).ConfigureAwait(false);
        }
        finally
        {
            audioStream.FrameReceived -= OnFrameReceived;
            inbound.Writer.TryComplete();
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

    private async Task PumpConsumerToCallAsync(CancellationToken token)
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
                        await audioStream.SendAsync(frame, token).ConfigureAwait(false);
                        break;

                    case MediaStreamEventType.Mark:
                        // Echo the marker back (Twilio semantics: fired once "reached").
                        await channel.SendAsync(message, token).ConfigureAwait(false);
                        break;

                    case MediaStreamEventType.Clear:
                        // Barge-in flush — no server-side outbound buffer in this slice (B4a-3).
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

    private static bool TryDecodePayload(string? base64, out ReadOnlyMemory<byte> frame)
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

        frame = default;
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
