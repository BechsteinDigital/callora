using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Application.Streaming.Protocol;
using Xunit;

namespace Callora.Core.Tests.Communication.Streaming;

/// <summary>
/// Duplex translation of the <see cref="MediaBridge"/> (B4a-2) against fake endpoints: it sends the
/// opening <c>start</c>, forwards consumer <c>media</c> to the call, echoes <c>mark</c>, ends on
/// <c>stop</c>, and forwards call audio back out as <c>media</c>.
/// </summary>
public sealed class MediaBridgeTests
{
    private static readonly MediaStreamStartMetadata Start = new("sess-1", "call-1", "audio/x-mulaw", 8000);

    [Fact]
    public async Task ConsumerToCall_ForwardsMedia_EchoesMark_StartsFirst_EndsOnStop()
    {
        var audio = new FakeCallAudioStream();
        var channel = new FakeMediaFrameChannel();
        channel.EnqueueInbound(MediaStreamMessage.Media(Convert.ToBase64String(new byte[] { 1, 2, 3 })));
        channel.EnqueueInbound(MediaStreamMessage.ForMark("m1"));
        channel.EnqueueInbound(MediaStreamMessage.Stop);

        await new MediaBridge(audio, channel).RunAsync(Start, CancellationToken.None);

        Assert.Equal(MediaStreamEventType.Start, channel.Sent[0].Event);
        Assert.Contains(audio.Sent, frame => frame.SequenceEqual(new byte[] { 1, 2, 3 }));
        Assert.Contains(channel.Sent, m => m.Event == MediaStreamEventType.Mark && m.MarkName == "m1");
    }

    [Fact]
    public async Task CallToConsumer_ForwardsInboundAudio_AsMediaFrames()
    {
        var audio = new FakeCallAudioStream();
        var channel = new FakeMediaFrameChannel();

        var run = new MediaBridge(audio, channel).RunAsync(Start, CancellationToken.None);
        audio.RaiseInbound(new byte[] { 9, 9 });
        await channel.FirstMediaSent.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains(channel.Sent, m =>
            m.Event == MediaStreamEventType.Media &&
            Convert.FromBase64String(m.Payload!).SequenceEqual(new byte[] { 9, 9 }));

        channel.CompleteInbound();
        await run;
    }
}

internal sealed class FakeCallAudioStream : ICallAudioStream
{
    public AudioFormat Format { get; } = AudioFormat.G711Ulaw8k20ms;

    public event EventHandler<AudioFrameReceivedEventArgs>? FrameReceived;

    public List<byte[]> Sent { get; } = [];

    public ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        Sent.Add(frame.ToArray());
        return ValueTask.CompletedTask;
    }

    public void RaiseInbound(byte[] frame) =>
        FrameReceived?.Invoke(this, new AudioFrameReceivedEventArgs(frame));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class FakeMediaFrameChannel : IMediaFrameChannel
{
    private readonly Channel<MediaStreamMessage> _incoming = Channel.CreateUnbounded<MediaStreamMessage>();
    private readonly TaskCompletionSource _firstMediaSent = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _gate = new();

    public List<MediaStreamMessage> Sent { get; } = [];

    public Task FirstMediaSent => _firstMediaSent.Task;

    public ValueTask SendAsync(MediaStreamMessage message, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            Sent.Add(message);
        }

        if (message.Event == MediaStreamEventType.Media)
        {
            _firstMediaSent.TrySetResult();
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<MediaStreamMessage?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _incoming.Reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    public void EnqueueInbound(MediaStreamMessage message) => _incoming.Writer.TryWrite(message);

    public void CompleteInbound() => _incoming.Writer.TryComplete();
}
