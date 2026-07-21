using System;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Streaming;
using Xunit;

namespace Callora.Core.Tests.Communication.Streaming;

/// <summary>
/// Domain invariants of <see cref="MediaStreamSession"/> (B4a): the connect token is single-use
/// (only a Pending session activates) and TTL-bounded, and Close is idempotent. These guard the
/// WS-connect authorizer that will validate tokens against this state.
/// </summary>
public sealed class MediaStreamSessionTests
{
    private static MediaStreamSession NewPending(DateTimeOffset createdAt) => new(
        id: "sess-1",
        callId: "call-1",
        workspaceKey: "ws-a",
        consumerRef: "ai-agent",
        connectToken: "tok-abc",
        format: AudioFormat.G711Ulaw8k20ms,
        direction: MediaStreamDirection.Bidirectional,
        createdAt: createdAt);

    [Fact]
    public void NewSession_IsPending()
    {
        var session = NewPending(DateTimeOffset.UnixEpoch);

        Assert.Equal(MediaStreamSessionStatus.Pending, session.Status);
        Assert.Null(session.StartedAt);
        Assert.Null(session.EndedAt);
    }

    [Fact]
    public void Activate_FromPending_GoesActiveAndStamps()
    {
        var session = NewPending(DateTimeOffset.UnixEpoch);

        session.Activate(DateTimeOffset.UnixEpoch.AddSeconds(3));

        Assert.Equal(MediaStreamSessionStatus.Active, session.Status);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(3), session.StartedAt);
    }

    [Fact]
    public void Activate_Twice_Throws_TokenIsSingleUse()
    {
        var session = NewPending(DateTimeOffset.UnixEpoch);
        session.Activate(DateTimeOffset.UnixEpoch.AddSeconds(1));

        Assert.Throws<InvalidOperationException>(() => session.Activate(DateTimeOffset.UnixEpoch.AddSeconds(2)));
    }

    [Fact]
    public void CanActivate_WithinTtl_True_AfterTtl_False()
    {
        var session = NewPending(DateTimeOffset.UnixEpoch);
        var ttl = TimeSpan.FromSeconds(30);

        Assert.True(session.CanActivate(DateTimeOffset.UnixEpoch.AddSeconds(10), ttl));
        Assert.False(session.CanActivate(DateTimeOffset.UnixEpoch.AddSeconds(31), ttl));
    }

    [Fact]
    public void CanActivate_AfterActivation_False()
    {
        var session = NewPending(DateTimeOffset.UnixEpoch);
        session.Activate(DateTimeOffset.UnixEpoch.AddSeconds(1));

        Assert.False(session.CanActivate(DateTimeOffset.UnixEpoch.AddSeconds(2), TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void Close_SetsClosedAndEndedAt_AndIsIdempotent()
    {
        var session = NewPending(DateTimeOffset.UnixEpoch);
        session.Activate(DateTimeOffset.UnixEpoch.AddSeconds(1));

        session.Close(DateTimeOffset.UnixEpoch.AddSeconds(5));
        var firstEnded = session.EndedAt;
        session.Close(DateTimeOffset.UnixEpoch.AddSeconds(9));

        Assert.Equal(MediaStreamSessionStatus.Closed, session.Status);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(5), firstEnded);
        Assert.Equal(firstEnded, session.EndedAt);
    }
}
