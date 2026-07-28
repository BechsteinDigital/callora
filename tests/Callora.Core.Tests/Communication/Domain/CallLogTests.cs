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

    [Fact]
    public void MarkAnswered_BeforeStart_Throws()
    {
        var log = Started();

        Assert.Throws<ArgumentOutOfRangeException>(() => log.MarkAnswered(Start.AddSeconds(-1)));
    }

    [Fact]
    public void MarkAnswered_Twice_Throws()
    {
        var log = Started();
        log.MarkAnswered(Start.AddSeconds(2));

        Assert.Throws<InvalidOperationException>(() => log.MarkAnswered(Start.AddSeconds(3)));
    }

    [Fact]
    public void MarkAnswered_AfterEnd_Throws()
    {
        var log = Started();
        log.End(Start.AddSeconds(5), CallOutcome.Missed, null);

        Assert.Throws<InvalidOperationException>(() => log.MarkAnswered(Start.AddSeconds(6)));
    }

    [Fact]
    public void End_Twice_Throws()
    {
        var log = Started();
        log.End(Start.AddSeconds(5), CallOutcome.Missed, null);

        Assert.Throws<InvalidOperationException>(() => log.End(Start.AddSeconds(6), CallOutcome.Failed, null));
    }

    [Fact]
    public void End_Unanswered_WithCompleted_Throws()
    {
        var log = Started();

        Assert.Throws<ArgumentException>(() => log.End(Start.AddSeconds(5), CallOutcome.Completed, null));
    }

    [Theory]
    [InlineData(CallOutcome.Missed)]
    [InlineData(CallOutcome.Rejected)]
    [InlineData(CallOutcome.Busy)]
    [InlineData(CallOutcome.NoAnswer)]
    [InlineData(CallOutcome.Canceled)]
    public void End_Answered_WithUnansweredOutcome_Throws(CallOutcome outcome)
    {
        var log = Started();
        log.MarkAnswered(Start.AddSeconds(2));

        Assert.Throws<ArgumentException>(() => log.End(Start.AddSeconds(5), outcome, null));
    }

    [Theory]
    [InlineData(CallOutcome.Missed)]
    [InlineData(CallOutcome.Rejected)]
    [InlineData(CallOutcome.Busy)]
    [InlineData(CallOutcome.NoAnswer)]
    [InlineData(CallOutcome.Canceled)]
    [InlineData(CallOutcome.Failed)]
    public void End_Unanswered_WithUnansweredOutcome_Succeeds(CallOutcome outcome)
    {
        var log = Started();

        log.End(Start.AddSeconds(5), outcome, null);

        Assert.Equal(outcome, log.Outcome);
        Assert.Equal(0, log.DurationSeconds);
    }

    [Fact]
    public void End_Answered_WithFailed_Succeeds()
    {
        var log = Started();
        log.MarkAnswered(Start.AddSeconds(2));

        log.End(Start.AddSeconds(8), CallOutcome.Failed, "timeout");

        Assert.Equal(CallOutcome.Failed, log.Outcome);
        Assert.Equal(6, log.DurationSeconds);
    }

    [Fact]
    public void End_BeforeStart_Throws()
    {
        var log = Started();

        Assert.Throws<ArgumentOutOfRangeException>(() => log.End(Start.AddSeconds(-1), CallOutcome.Missed, null));
    }

    [Fact]
    public void End_Answered_BeforeAnswerTime_Throws()
    {
        var log = Started();
        log.MarkAnswered(Start.AddSeconds(10));

        Assert.Throws<ArgumentOutOfRangeException>(() => log.End(Start.AddSeconds(5), CallOutcome.Completed, null));
    }

    [Fact]
    public void MarkAnswered_ExactlyAtStart_IsAllowed()
    {
        var log = Started();

        log.MarkAnswered(Start);

        Assert.Equal(Start, log.AnsweredAt);
    }

    [Fact]
    public void End_ExactlyAtAnswerTime_YieldsZeroDuration()
    {
        var log = Started();
        log.MarkAnswered(Start.AddSeconds(4));

        log.End(Start.AddSeconds(4), CallOutcome.Completed, null);

        Assert.Equal(0, log.DurationSeconds);
    }
}
