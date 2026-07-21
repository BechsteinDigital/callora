using System;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Calls;
using Xunit;

namespace Callora.Core.Tests.Communication.Domain;

public sealed class CallLogTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    private static CallLog Started() =>
        CallLog.Start("call-1", "ws-a", "acc-1", "line-1", CallDirection.Inbound,
            "+49309999999", "sip:alice@example.org", handledBy: "ai-agent", correlationId: null, Start);

    [Fact]
    public void Start_IsInProgress_WithoutEnd()
    {
        var log = Started();

        Assert.Equal(CallOutcome.InProgress, log.Outcome);
        Assert.Null(log.EndedAt);
        Assert.Equal(0, log.DurationSeconds);
    }

    [Fact]
    public void End_Answered_ComputesTalkTime()
    {
        var log = Started();
        log.MarkAnswered(Start.AddSeconds(3));

        log.End(Start.AddSeconds(63), CallOutcome.Completed, disconnectCause: "BYE");

        Assert.Equal(CallOutcome.Completed, log.Outcome);
        Assert.Equal(60, log.DurationSeconds);
        Assert.Equal("BYE", log.DisconnectCause);
    }

    [Fact]
    public void End_NeverAnswered_DurationZero()
    {
        var log = Started();

        log.End(Start.AddSeconds(20), CallOutcome.Missed, disconnectCause: null);

        Assert.Equal(CallOutcome.Missed, log.Outcome);
        Assert.Equal(0, log.DurationSeconds);
    }

    [Fact]
    public void End_InProgressOutcome_Throws()
    {
        var log = Started();

        Assert.Throws<ArgumentException>(() =>
            log.End(Start.AddSeconds(5), CallOutcome.InProgress, null));
    }

    [Fact]
    public void Pseudonymize_ReplacesRemoteParty()
    {
        var log = Started();

        log.Pseudonymize("REDACTED");

        Assert.Equal("REDACTED", log.RemoteParty);
    }
}
