using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Application.Streaming.Pacing;
using Xunit;

namespace Callora.Core.Tests.Communication.Streaming;

/// <summary>
/// Pacing semantics of <see cref="PacedAudioSender"/> (B4a-3a): one queued frame is emitted per
/// clock tick and in order; <see cref="PacedAudioSender.Flush"/> drops queued audio (barge-in);
/// the buffer is capped, dropping the oldest. Driven by <see cref="ManualPacingClock"/> — fully
/// deterministic, no wall-clock waits.
/// </summary>
public sealed class PacedAudioSenderTests
{
    [Fact]
    public async Task Emits_OneFramePerTick_InOrder()
    {
        var clock = new ManualPacingClock();
        var sink = new CapturingSink();
        var sender = new PacedAudioSender(sink.SendAsync, clock);
        using var cts = new CancellationTokenSource();
        var run = sender.RunAsync(cts.Token);

        sender.Enqueue([1]);
        sender.Enqueue([2]);

        clock.Tick();
        Assert.Equal(new byte[] { 1 }, await sink.ReadAsync());
        clock.Tick();
        Assert.Equal(new byte[] { 2 }, await sink.ReadAsync());

        cts.Cancel();
        await Observe(run);
    }

    [Fact]
    public async Task Flush_DropsQueuedAudio_ThenEmitsOnlyNewFrames()
    {
        var clock = new ManualPacingClock();
        var sink = new CapturingSink();
        var sender = new PacedAudioSender(sink.SendAsync, clock);
        using var cts = new CancellationTokenSource();
        var run = sender.RunAsync(cts.Token);

        sender.Enqueue([1]);
        sender.Enqueue([2]);
        sender.Flush(); // barge-in: 1 and 2 are dropped before any tick
        sender.Enqueue([3]);

        clock.Tick();
        Assert.Equal(new byte[] { 3 }, await sink.ReadAsync());

        cts.Cancel();
        await Observe(run);
    }

    [Fact]
    public async Task Cap_DropsOldest_WhenBufferExceeded()
    {
        var clock = new ManualPacingClock();
        var sink = new CapturingSink();
        var sender = new PacedAudioSender(sink.SendAsync, clock, maxBufferedFrames: 2);
        using var cts = new CancellationTokenSource();
        var run = sender.RunAsync(cts.Token);

        sender.Enqueue([1]);
        sender.Enqueue([2]);
        sender.Enqueue([3]); // exceeds cap of 2 → oldest ([1]) dropped

        clock.Tick();
        Assert.Equal(new byte[] { 2 }, await sink.ReadAsync());
        clock.Tick();
        Assert.Equal(new byte[] { 3 }, await sink.ReadAsync());

        cts.Cancel();
        await Observe(run);
    }

    private static async Task Observe(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }
}

internal sealed class CapturingSink
{
    private readonly Channel<byte[]> _sent = Channel.CreateUnbounded<byte[]>();

    public ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        _sent.Writer.TryWrite(frame.ToArray());
        return ValueTask.CompletedTask;
    }

    public async Task<byte[]> ReadAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return await _sent.Reader.ReadAsync(timeout.Token);
    }
}
