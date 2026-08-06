using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Application.Conference;
using Callora.Core.Tests.Communication.Streaming;
using Callora.Plugin.Communication.Infrastructure.RealtimeMedia;
using Xunit;

namespace Callora.Core.Tests.Communication.Conference;

/// <summary>
/// Drives the downlink at the endpoint's frame cadence. Unlike the queue-fed
/// <c>PacedAudioSender</c>, there is nothing to buffer here: the mix is produced on demand, one frame
/// per tick, so a slow or absent participant costs a quieter frame rather than a backlog.
/// </summary>
public sealed class ConferenceDownlinkPumpTests
{
    [Fact]
    public async Task EachTick_SendsExactlyOneFrame()
    {
        var clock = new ManualPacingClock();
        var sent = new List<byte[]>();
        using var mixer = NewMixer();
        var pump = new ConferenceDownlinkPump(mixer, (frame, _) => { sent.Add(frame.ToArray()); return ValueTask.CompletedTask; }, clock);
        using var cts = new CancellationTokenSource();
        var run = pump.RunAsync(cts.Token);

        clock.Tick();
        clock.Tick();
        await WaitUntil(() => sent.Count == 2);

        await cts.CancelAsync();
        await run;
        Assert.Equal(2, sent.Count);
        Assert.All(sent, frame => Assert.Equal(160, frame.Length));
    }

    [Fact]
    public async Task AFailedSend_DoesNotStopThePump()
    {
        var clock = new ManualPacingClock();
        var attempts = 0;
        using var mixer = NewMixer();
        var pump = new ConferenceDownlinkPump(
            mixer,
            (_, _) =>
            {
                attempts++;
                return attempts == 1
                    ? ValueTask.FromException(new InvalidOperationException("transport hiccup"))
                    : ValueTask.CompletedTask;
            },
            clock);
        using var cts = new CancellationTokenSource();
        var run = pump.RunAsync(cts.Token);

        // One dropped frame is 20 ms of audio; tearing the leg down over it would drop the whole call.
        clock.Tick();
        clock.Tick();
        await WaitUntil(() => attempts == 2);

        await cts.CancelAsync();
        await run;
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task WhileAnAnnouncementIsPlaying_ThePumpStandsStill()
    {
        var clock = new ManualPacingClock();
        var sent = 0;
        var announcing = true;
        using var mixer = NewMixer();
        var pump = new ConferenceDownlinkPump(
            mixer, (_, _) => { sent++; return ValueTask.CompletedTask; }, clock, isSuppressed: () => announcing);
        using var cts = new CancellationTokenSource();
        var run = pump.RunAsync(cts.Token);

        clock.Tick();
        clock.Tick();
        await Task.Delay(50);
        var duringAnnouncement = sent;

        announcing = false;
        clock.Tick();
        await WaitUntil(() => sent > duringAnnouncement);

        await cts.CancelAsync();
        await run;

        // Two senders on one stream interleave their frames, and the result is neither the room nor
        // the announcement — it is both, chopped. The announcement takes the path for its duration.
        Assert.Equal(0, duringAnnouncement);
        Assert.True(sent > 0);
    }

    [Fact]
    public async Task Cancellation_EndsTheLoopWithoutThrowing()
    {
        var clock = new ManualPacingClock();
        using var mixer = NewMixer();
        var pump = new ConferenceDownlinkPump(mixer, (_, _) => ValueTask.CompletedTask, clock);
        using var cts = new CancellationTokenSource();

        var run = pump.RunAsync(cts.Token);
        await cts.CancelAsync();

        await run; // a hung-up call is an ordinary ending, not a fault
    }

    private static ConferenceDownlinkMixer NewMixer() =>
        new(new SdkAudioTranscoderFactory(), ConferenceAudioCodec.Opus, ConferenceAudioCodec.G711Ulaw, 8_000, 160);

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }
}
