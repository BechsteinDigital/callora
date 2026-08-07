using System.Linq;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Calls;
using Xunit;

namespace Callora.Core.Tests.Communication.Calls;

/// <summary>
/// What actually happened to one call. Today a call that goes nowhere leaves a history row saying it
/// ended and nothing about why — an operator asking "why did this ring out" has only the log file of
/// whichever plugin happened to be involved, if any.
/// </summary>
public sealed class CallJourneyTests
{
    private const string Workspace = "ws-a";
    private const string CallId = "call-1";

    [Fact]
    public void StepsComeBackInTheOrderTheyHappened()
    {
        // A journey read out of order is worse than none: the whole value is the sequence.
        var journey = new CallJourney();

        journey.Record(Workspace, CallId, new CallJourneyStep("communication", "call.ringing"));
        journey.Record(Workspace, CallId, new CallJourneyStep("videoconference", "dial-in.claimed"));
        journey.Record(Workspace, CallId, new CallJourneyStep("videoconference", "room.attached"));

        Assert.Equal(
            ["call.ringing", "dial-in.claimed", "room.attached"],
            journey.Read(Workspace, CallId).Select(s => s.Step));
    }

    [Fact]
    public void ACallNobodyRecordedAnythingFor_HasAnEmptyJourney()
    {
        Assert.Empty(new CallJourney().Read(Workspace, CallId));
    }

    [Fact]
    public void JourneysOfDifferentCalls_DoNotMix()
    {
        var journey = new CallJourney();

        journey.Record(Workspace, "call-1", new CallJourneyStep("communication", "call.ringing"));
        journey.Record(Workspace, "call-2", new CallJourneyStep("communication", "call.rejected"));

        Assert.Equal("call.ringing", Assert.Single(journey.Read(Workspace, "call-1")).Step);
    }

    [Fact]
    public void TheSameCallIdInTwoWorkspaces_StaysApart()
    {
        // Call ids are unique within a channel, not across the deployment.
        var journey = new CallJourney();

        journey.Record("ws-a", CallId, new CallJourneyStep("communication", "call.ringing"));
        journey.Record("ws-b", CallId, new CallJourneyStep("communication", "call.rejected"));

        Assert.Equal("call.rejected", Assert.Single(journey.Read("ws-b", CallId)).Step);
    }

    [Fact]
    public void TakingTheJourney_EmptiesIt()
    {
        // Taken when the call ends and written onto its history row. Leaving it behind would grow the
        // buffer by every call the process ever saw.
        var journey = new CallJourney();
        journey.Record(Workspace, CallId, new CallJourneyStep("communication", "call.ringing"));

        Assert.Single(journey.Take(Workspace, CallId));
        Assert.Empty(journey.Read(Workspace, CallId));
    }

    [Fact]
    public void ALongCall_StopsGrowingAndSaysSo()
    {
        // A flow that loops — a caller wandering a menu for an hour — must not turn one call into
        // unbounded memory. Truncating silently would be worse: the tail is where the failure is.
        var journey = new CallJourney(maxSteps: 3);

        for (var i = 0; i < 10; i++)
        {
            journey.Record(Workspace, CallId, new CallJourneyStep("communication", $"step-{i}"));
        }

        var steps = journey.Read(Workspace, CallId);
        Assert.Equal(4, steps.Count);
        Assert.Equal(CallJourney.TruncatedStep, steps[^1].Step);
    }

    [Fact]
    public void RecordingIsSafeFromAnyThread()
    {
        // Steps arrive from signalling and media threads at once; a torn list would be a crash on the
        // call path, which is the one place nothing may crash.
        var journey = new CallJourney();

        Parallel.For(0, 200, i =>
            journey.Record(Workspace, CallId, new CallJourneyStep("communication", $"step-{i}")));

        Assert.Equal(200, journey.Read(Workspace, CallId).Count);
    }

    [Fact]
    public void AStepWithoutASource_IsRejectedAtTheContract()
    {
        // Every step says who recorded it, or the journey stops being readable as a story.
        Assert.Throws<ArgumentException>(() => new CallJourneyStep("  ", "call.ringing"));
        Assert.Throws<ArgumentException>(() => new CallJourneyStep("communication", " "));
    }

    [Fact]
    public void AFinishedCallCarriesItsJourneyOnItsHistoryRecord()
    {
        // Where an operator looks afterwards. The buffer is for the call that is still running.
        var log = Callora.Plugin.Communication.Domain.Calls.CallLog.Start(
            CallId, Workspace, "acc-1", CallDirection.Inbound, "+4917012345678", "+493012345678",
            handledBy: null, correlationId: null, startedAt: DateTimeOffset.UnixEpoch);

        log.RecordJourney([new CallJourneyStep("communication", "call.ringing")]);

        Assert.Equal("call.ringing", Assert.Single(log.Journey).Step);
    }

    [Fact]
    public void AHistoryRecordNobodyWroteAJourneyFor_ReadsAsEmptyRatherThanNull()
    {
        // Every row that predates this column is in exactly that state.
        var log = Callora.Plugin.Communication.Domain.Calls.CallLog.Start(
            CallId, Workspace, "acc-1", CallDirection.Inbound, "+4917012345678", "+493012345678",
            handledBy: null, correlationId: null, startedAt: DateTimeOffset.UnixEpoch);

        Assert.Empty(log.Journey);
    }

    [Fact]
    public void EachStepCarriesWhenItHappened()
    {
        var journey = new CallJourney();
        journey.Record(Workspace, CallId, new CallJourneyStep("communication", "call.ringing"));

        Assert.NotEqual(default, Assert.Single(journey.Read(Workspace, CallId)).At);
    }
}
