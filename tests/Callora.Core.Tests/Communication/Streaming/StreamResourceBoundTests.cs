using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Application.Streaming.Pacing;
using Callora.Plugin.Communication.Application.Streaming.Protocol;
using Callora.Plugin.Communication.Domain.Streaming;
using Xunit;

namespace Callora.Core.Tests.Communication.Streaming;

/// <summary>
/// A valid ticket holder is still an untrusted peer (#108). These tests drive the
/// abusive shapes — oversized payloads, wrong frame sizes, queue pressure, replayed
/// and future-dated tickets — and assert the bound holds.
/// </summary>
public sealed class StreamResourceBoundTests
{
    private static readonly MediaStreamStartMetadata Start = new("sess-1", "call-1", "audio/x-mulaw", 8000);
    private static readonly int FrameBytes = AudioFormat.G711Ulaw8k20ms.BytesPerFrame;

    [Fact]
    public async Task OversizedAudioPayload_IsDropped_WithoutReachingTheCall()
    {
        var clock = new ManualPacingClock();
        var audio = new FakeCallAudioStream();
        var channel = new FakeMediaFrameChannel();

        // Far beyond any legitimate frame: must never be decoded into the call.
        var oversized = new byte[CommunicationStreamLimits.MaxAudioFrameBytes * 4];
        channel.EnqueueInbound(MediaStreamMessage.Media(Convert.ToBase64String(oversized)));
        channel.EnqueueInbound(MediaStreamMessage.ForMark("done"));

        var run = new MediaBridge(audio, channel, clock).RunAsync(Start, CancellationToken.None);
        await channel.MarkEchoed("done").WaitAsync(TimeSpan.FromSeconds(5));

        clock.Tick();
        Assert.Empty(audio.Sent);

        channel.CompleteInbound();
        await run;
    }

    [Fact]
    public async Task FrameOfTheWrongSize_IsDropped()
    {
        var clock = new ManualPacingClock();
        var audio = new FakeCallAudioStream();
        var channel = new FakeMediaFrameChannel();

        // Within the byte cap, but not the negotiated frame length.
        channel.EnqueueInbound(MediaStreamMessage.Media(Convert.ToBase64String(new byte[FrameBytes - 1])));
        channel.EnqueueInbound(MediaStreamMessage.ForMark("done"));

        var run = new MediaBridge(audio, channel, clock).RunAsync(Start, CancellationToken.None);
        await channel.MarkEchoed("done").WaitAsync(TimeSpan.FromSeconds(5));

        clock.Tick();
        Assert.Empty(audio.Sent);

        channel.CompleteInbound();
        await run;
    }

    [Fact]
    public void PacedSender_IsBoundedByBytes_NotOnlyByFrameCount()
    {
        // A frame count high enough that the count cap never trips: only the byte cap can
        // hold this. Without it, a producer sending large frames grows the buffer freely.
        var sender = new PacedAudioSender(
            (_, _) => ValueTask.CompletedTask,
            new ManualPacingClock(),
            maxBufferedFrames: 100_000,
            maxBufferedBytes: 4096);

        for (var i = 0; i < 100; i++)
        {
            sender.Enqueue(new byte[1024]);
        }

        Assert.True(
            sender.BufferedBytes <= 4096,
            $"buffered {sender.BufferedBytes} bytes, cap is 4096");
    }

    [Fact]
    public void PacedSender_FlushReleasesTheAccountedBytes()
    {
        var sender = new PacedAudioSender((_, _) => ValueTask.CompletedTask, new ManualPacingClock());
        sender.Enqueue(new byte[FrameBytes]);
        Assert.Equal(FrameBytes, sender.BufferedBytes);

        sender.Flush();

        Assert.Equal(0, sender.BufferedBytes);
    }

    [Fact]
    public void ConnectToken_IsStoredOnlyAsAHash()
    {
        var session = NewSession("secret-connect-token", DateTimeOffset.UtcNow);

        Assert.DoesNotContain("secret-connect-token", session.ConnectTokenHash, StringComparison.Ordinal);
        Assert.Equal(MediaStreamSession.HashToken("secret-connect-token"), session.ConnectTokenHash);
        Assert.Equal(64, session.ConnectTokenHash.Length);
    }

    [Fact]
    public void FutureDatedSession_CannotBeActivated()
    {
        var now = DateTimeOffset.UtcNow;
        // A row dated into the future satisfies a bare "not older than TTL" check forever.
        var session = NewSession("token", now.AddDays(1));

        Assert.False(session.CanActivate(now, CommunicationStreamLimits.ConnectTokenTimeToLive));
    }

    [Fact]
    public void ExpiredSession_CannotBeActivated()
    {
        var now = DateTimeOffset.UtcNow;
        var session = NewSession("token", now - CommunicationStreamLimits.ConnectTokenTimeToLive.Add(TimeSpan.FromSeconds(1)));

        Assert.False(session.CanActivate(now, CommunicationStreamLimits.ConnectTokenTimeToLive));
    }

    [Fact]
    public void ActivatedSession_CannotBeActivatedAgain()
    {
        var now = DateTimeOffset.UtcNow;
        var session = NewSession("token", now);
        session.Activate(now);

        Assert.False(session.CanActivate(now, CommunicationStreamLimits.ConnectTokenTimeToLive));
        Assert.Throws<InvalidOperationException>(() => session.Activate(now));
    }

    [Fact]
    public void ClosedSession_BecomesPurgeableAfterRetention()
    {
        var now = DateTimeOffset.UtcNow;
        var session = NewSession("token", now.AddDays(-2));
        session.Activate(now.AddDays(-2));
        session.Close(now.AddDays(-2));

        Assert.True(session.CanPurge(now, CommunicationStreamLimits.SessionRetention));
    }

    [Fact]
    public void FreshSession_IsNotPurgeable()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.False(NewSession("token", now).CanPurge(now, CommunicationStreamLimits.SessionRetention));
    }

    private static MediaStreamSession NewSession(string token, DateTimeOffset createdAt) => new(
        "sess-1",
        "call-1",
        "workspace-a",
        "ai-agent",
        token,
        AudioFormat.G711Ulaw8k20ms,
        MediaStreamDirection.Bidirectional,
        createdAt);
}
