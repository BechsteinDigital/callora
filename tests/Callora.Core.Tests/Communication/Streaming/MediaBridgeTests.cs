using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Application.Streaming.Pacing;
using Callora.Plugin.Communication.Application.Streaming.Protocol;
using Xunit;

namespace Callora.Core.Tests.Communication.Streaming;

/// <summary>
/// Duplex translation of the <see cref="MediaBridge"/> with pacing (B4a-3a): it sends the opening
/// <c>start</c>, paces consumer <c>media</c> to the call one frame per clock tick, drops queued
/// audio on <c>clear</c> (barge-in), and forwards call audio out as <c>media</c>. Timing is
/// deterministic via a manual clock and a mark-echo sync barrier — no wall-clock waits.
/// </summary>
public sealed class MediaBridgeTests
{
    private static readonly MediaStreamStartMetadata Start = new("sess-1", "call-1", "audio/x-mulaw", 8000);

    [Fact]
    public async Task Start_IsSentFirst_And_ConsumerMedia_IsPacedToCall_OneFramePerTick()
    {
        var clock = new ManualPacingClock();
        var audio = new FakeCallAudioStream();
        var channel = new FakeMediaFrameChannel();
        channel.EnqueueInbound(MediaStreamMessage.Media(Convert.ToBase64String(new byte[] { 1, 2, 3 })));
        channel.EnqueueInbound(MediaStreamMessage.ForMark("queued"));

        var run = new MediaBridge(audio, channel, clock).RunAsync(Start, CancellationToken.None);
        await channel.MarkEchoed("queued").WaitAsync(TimeSpan.FromSeconds(5)); // frame is now buffered in the pacer

        Assert.Equal(MediaStreamEventType.Start, channel.Sent[0].Event);
        Assert.Empty(audio.Sent); // nothing paced out until a tick

        clock.Tick();
        await audio.FirstSent.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(audio.Sent);
        Assert.Equal(new byte[] { 1, 2, 3 }, audio.Sent[0]);

        channel.CompleteInbound();
        await run;
    }

    [Fact]
    public async Task Clear_FlushesQueuedAudio_BargeIn_KeepsOnlyPostClearFrame()
    {
        var clock = new ManualPacingClock();
        var audio = new FakeCallAudioStream();
        var channel = new FakeMediaFrameChannel();
        channel.EnqueueInbound(MediaStreamMessage.Media(Convert.ToBase64String(new byte[] { 1 })));
        channel.EnqueueInbound(MediaStreamMessage.Media(Convert.ToBase64String(new byte[] { 2 })));
        channel.EnqueueInbound(MediaStreamMessage.Clear);
        channel.EnqueueInbound(MediaStreamMessage.Media(Convert.ToBase64String(new byte[] { 3 })));
        channel.EnqueueInbound(MediaStreamMessage.ForMark("ready"));

        var run = new MediaBridge(audio, channel, clock).RunAsync(Start, CancellationToken.None);
        await channel.MarkEchoed("ready").WaitAsync(TimeSpan.FromSeconds(5)); // f1,f2 enqueued+flushed, f3 enqueued

        clock.Tick();
        await audio.FirstSent.WaitAsync(TimeSpan.FromSeconds(5));

        // Only the post-clear frame survives — the pre-clear playback was dropped.
        Assert.Single(audio.Sent);
        Assert.Equal(new byte[] { 3 }, audio.Sent[0]);

        channel.CompleteInbound();
        await run;
    }

    [Fact]
    public async Task CallToConsumer_ForwardsInboundAudio_AsMediaFrames()
    {
        var clock = new ManualPacingClock();
        var audio = new FakeCallAudioStream();
        var channel = new FakeMediaFrameChannel();

        var run = new MediaBridge(audio, channel, clock).RunAsync(Start, CancellationToken.None);
        audio.RaiseInbound(new byte[] { 9, 9 });
        await channel.FirstMediaSent.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains(channel.Sent, m =>
            m.Event == MediaStreamEventType.Media &&
            Convert.FromBase64String(m.Payload!).SequenceEqual(new byte[] { 9, 9 }));

        channel.CompleteInbound();
        await run;
    }

    [Fact]
    public async Task Stop_EndsTheStream_And_Mark_IsEchoedBack()
    {
        var clock = new ManualPacingClock();
        var audio = new FakeCallAudioStream();
        var channel = new FakeMediaFrameChannel();
        channel.EnqueueInbound(MediaStreamMessage.ForMark("beep"));
        channel.EnqueueInbound(MediaStreamMessage.Stop);

        // The stop frame (not CompleteInbound) terminates the bridge on its own.
        await new MediaBridge(audio, channel, clock).RunAsync(Start, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains(channel.Sent, m => m.Event == MediaStreamEventType.Mark && m.MarkName == "beep");
    }
}

internal sealed class ManualPacingClock : IPacingClock
{
    private readonly Channel<bool> _ticks = Channel.CreateUnbounded<bool>();

    public void Tick() => _ticks.Writer.TryWrite(true);

    public async ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _ticks.Reader.ReadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (ChannelClosedException)
        {
            return false;
        }
    }
}

internal sealed class FakeCallAudioStream : ICallAudioStream
{
    private readonly TaskCompletionSource _firstSent = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AudioFormat Format { get; } = AudioFormat.G711Ulaw8k20ms;

    public event EventHandler<AudioFrameReceivedEventArgs>? FrameReceived;

    public List<byte[]> Sent { get; } = [];

    public Task FirstSent => _firstSent.Task;

    public ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        Sent.Add(frame.ToArray());
        _firstSent.TrySetResult();
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
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _markEchoes = new();
    private readonly object _gate = new();

    public List<MediaStreamMessage> Sent { get; } = [];

    public Task FirstMediaSent => _firstMediaSent.Task;

    public Task MarkEchoed(string name) =>
        _markEchoes.GetOrAdd(name, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

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
        else if (message.Event == MediaStreamEventType.Mark && message.MarkName is { } name)
        {
            _markEchoes.GetOrAdd(name, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
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
