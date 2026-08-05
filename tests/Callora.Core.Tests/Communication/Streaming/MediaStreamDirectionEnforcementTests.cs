using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Application.Streaming.Protocol;
using Callora.Plugin.Communication.Domain.Streaming;
using Xunit;

namespace Callora.Core.Tests.Communication.Streaming;

/// <summary>
/// The session's direction used to be recorded and then ignored, so every ticket was effectively
/// duplex (#114). The bridge is where both audio paths meet, so it is where the direction has to be
/// enforced — a listen-only consumer's frames must not reach the call, and a speak-only consumer
/// must not receive call audio.
/// </summary>
public sealed class MediaStreamDirectionEnforcementTests
{
    private static readonly MediaStreamStartMetadata Start = new("sess-1", "call-1", "audio/x-mulaw", 8000);

    [Fact]
    public async Task ListenOnly_ReceivesCallAudio()
    {
        var audio = new FakeCallAudioStream();
        var channel = new FakeMediaFrameChannel();
        var clock = new ManualPacingClock();
        channel.EnqueueInbound(MediaStreamMessage.ForMark("ready"));

        var run = new MediaBridge(audio, channel, MediaStreamDirection.Inbound, clock).RunAsync(Start, CancellationToken.None);

        // The echoed mark means the pumps are running, which in turn means the bridge has already
        // subscribed to call audio — so the frame raised next cannot be missed.
        await channel.MarkEchoed("ready").WaitAsync(TimeSpan.FromSeconds(5));
        audio.RaiseInbound(Frame(9));
        await channel.FirstMediaSent.WaitAsync(TimeSpan.FromSeconds(5));

        channel.CompleteInbound();
        await run;

        var forwarded = channel.Sent.Single(x => x.Event == MediaStreamEventType.Media);
        Assert.Equal(Frame(9), Convert.FromBase64String(forwarded.Payload!));
    }

    [Fact]
    public async Task ListenOnly_CannotSpeakIntoTheCall()
    {
        var audio = new FakeCallAudioStream();
        var channel = new FakeMediaFrameChannel();
        var clock = new ManualPacingClock();
        channel.EnqueueInbound(MediaStreamMessage.Media(Convert.ToBase64String(Frame(1))));
        channel.EnqueueInbound(MediaStreamMessage.ForMark("processed"));

        var run = new MediaBridge(audio, channel, MediaStreamDirection.Inbound, clock).RunAsync(Start, CancellationToken.None);

        // The echoed mark proves the frame ahead of it was consumed, so "nothing was paced out" is
        // an observation rather than a race.
        await channel.MarkEchoed("processed").WaitAsync(TimeSpan.FromSeconds(5));
        clock.Tick();
        channel.CompleteInbound();
        await run;

        Assert.Empty(audio.Sent);
    }

    [Fact]
    public async Task SpeakOnly_ReachesTheCall()
    {
        var audio = new FakeCallAudioStream();
        var channel = new FakeMediaFrameChannel();
        var clock = new ManualPacingClock();
        channel.EnqueueInbound(MediaStreamMessage.Media(Convert.ToBase64String(Frame(2))));
        channel.EnqueueInbound(MediaStreamMessage.ForMark("queued"));

        var run = new MediaBridge(audio, channel, MediaStreamDirection.Outbound, clock).RunAsync(Start, CancellationToken.None);
        await channel.MarkEchoed("queued").WaitAsync(TimeSpan.FromSeconds(5));
        clock.Tick();
        await audio.FirstSent.WaitAsync(TimeSpan.FromSeconds(5));

        channel.CompleteInbound();
        await run;

        Assert.Equal(Frame(2), Assert.Single(audio.Sent));
    }

    [Fact]
    public async Task SpeakOnly_NeverReceivesCallAudio()
    {
        var audio = new FakeCallAudioStream();
        var channel = new FakeMediaFrameChannel();
        var clock = new ManualPacingClock();
        channel.EnqueueInbound(MediaStreamMessage.ForMark("running"));

        var run = new MediaBridge(audio, channel, MediaStreamDirection.Outbound, clock).RunAsync(Start, CancellationToken.None);
        await channel.MarkEchoed("running").WaitAsync(TimeSpan.FromSeconds(5));
        audio.RaiseInbound(Frame(9));

        // A second round-trip after the call side spoke: if that frame were going to be forwarded,
        // it would have been by the time this mark comes back.
        channel.EnqueueInbound(MediaStreamMessage.ForMark("after"));
        await channel.MarkEchoed("after").WaitAsync(TimeSpan.FromSeconds(5));
        channel.CompleteInbound();
        await run;

        Assert.DoesNotContain(channel.Sent, x => x.Event == MediaStreamEventType.Media);
    }

    [Fact]
    public async Task ListenOnly_StillHandlesControlFrames()
    {
        // Direction bounds audio, not control: a listener still needs stop and mark to end its
        // stream cleanly and to synchronize with the server.
        var audio = new FakeCallAudioStream();
        var channel = new FakeMediaFrameChannel();
        var clock = new ManualPacingClock();
        channel.EnqueueInbound(MediaStreamMessage.ForMark("ping"));

        var run = new MediaBridge(audio, channel, MediaStreamDirection.Inbound, clock).RunAsync(Start, CancellationToken.None);
        await channel.MarkEchoed("ping").WaitAsync(TimeSpan.FromSeconds(5));

        channel.EnqueueInbound(MediaStreamMessage.Stop);
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains(channel.Sent, x => x is { Event: MediaStreamEventType.Mark, MarkName: "ping" });
    }

    private static byte[] Frame(byte marker)
    {
        var frame = new byte[AudioFormat.G711Ulaw8k20ms.BytesPerFrame];
        frame[0] = marker;
        return frame;
    }
}
