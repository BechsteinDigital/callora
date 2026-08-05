using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Domain.Calls;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Communication.Calls;

/// <summary>
/// The lifecycle guarantees a live call stack needs (#113): identity that survives two channels
/// using the same call id, transitions that cannot interleave or replay, compensation when the
/// call is live but untrackable, and finalization at shutdown.
/// </summary>
public sealed class CallLifecycleSerializationTests
{
    private const string Workspace = "ws-a";

    [Fact]
    public async Task TwoChannels_WithTheSameCallId_AreTrackedSeparately()
    {
        // A provider's call id is unique inside its own channel. Keying by call id alone let the
        // second registration overwrite the first, handing one channel's hangup to the other.
        var registry = new CommunicationChannelRegistry();
        var store = new RecordingCallLogStore();
        await using var service = new CallControlService(
            registry, store, NullLogger<CallControlService>.Instance, TimeProvider.System);

        var firstCall = new ControllableCall("shared-id");
        var secondCall = new ControllableCall("shared-id");
        var firstChannel = new FakeCommunicationChannel { ChannelId = "ch-1", NextCall = firstCall };
        var secondChannel = new FakeCommunicationChannel { ChannelId = "ch-2", NextCall = secondCall };
        registry.Register(Workspace, firstChannel);
        registry.Register(Workspace, secondChannel);

        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567", ChannelId: "ch-1"));
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234568", ChannelId: "ch-2"));

        // Both calls remain live and independently finalizable.
        firstCall.Transition(CallState.Terminated);
        Assert.Equal(CallOutcome.Failed, store.Added[0].Outcome);
        Assert.Equal(CallOutcome.InProgress, store.Added[1].Outcome);

        secondCall.Transition(CallState.Terminated);
        Assert.Equal(CallOutcome.Failed, store.Added[1].Outcome);
    }

    [Fact]
    public async Task DuplicateTerminatedCallbacks_FinalizeExactlyOnce()
    {
        var (service, _, store, call) = await PlacedCallAsync();

        call.Transition(CallState.Connected);
        call.Transition(CallState.Terminated);
        var updatesAfterFirstEnd = store.UpdateCount;
        call.Transition(CallState.Terminated);

        Assert.Equal(updatesAfterFirstEnd, store.UpdateCount);
        Assert.Single(store.OutboxEntries, x => x.EventName == CallEventTypes.Ended);

        await service.DisposeAsync();
    }

    [Fact]
    public async Task ConnectedAfterTerminated_IsIgnored()
    {
        // Reordered provider callbacks must not produce a call that was answered after it ended.
        var (service, _, store, call) = await PlacedCallAsync();

        call.Transition(CallState.Terminated);
        call.Transition(CallState.Connected);

        Assert.NotNull(store.Added[0].EndedAt);
        Assert.Null(store.Added[0].AnsweredAt);
        Assert.DoesNotContain(store.OutboxEntries, x => x.EventName == CallEventTypes.StateChanged);

        await service.DisposeAsync();
    }

    [Fact]
    public async Task ConcurrentTerminatedCallbacks_ProduceOneFinalization()
    {
        var (service, _, store, call) = await PlacedCallAsync();

        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => call.Transition(CallState.Terminated))));

        Assert.Single(store.OutboxEntries, x => x.EventName == CallEventTypes.Ended);

        await service.DisposeAsync();
    }

    [Fact]
    public async Task EveryTransition_WritesItsEventWithTheLogChange()
    {
        // The outbox entry and the log change share one write, which is what makes an event
        // unable to describe a state the database does not hold.
        var (service, _, store, call) = await PlacedCallAsync();

        call.Transition(CallState.Connected);
        call.Transition(CallState.Terminated);

        Assert.Equal(
            [CallEventTypes.Placed, CallEventTypes.StateChanged, CallEventTypes.Ended],
            store.OutboxEntries.Select(x => x.EventName));
        Assert.All(store.OutboxEntries, x => Assert.Equal(Workspace, x.WorkspaceKey));
        // Distinct ids give a consumer something to deduplicate a redelivery on.
        Assert.Equal(3, store.OutboxEntries.Select(x => x.Id).Distinct().Count());

        await service.DisposeAsync();
    }

    [Fact]
    public async Task PersistenceFailureAfterDialing_HangsTheCallUp()
    {
        // The call is already live at the carrier. Reporting a failure while leaving it up would
        // bill for a call nobody can see or hang up.
        var registry = new CommunicationChannelRegistry();
        var store = new RecordingCallLogStore { AddFailure = new InvalidOperationException("database unreachable") };
        await using var service = new CallControlService(
            registry, store, NullLogger<CallControlService>.Instance, TimeProvider.System);

        var call = new ControllableCall("call-1");
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567")));

        Assert.Equal(CallState.Terminated, call.State);
        Assert.Null(service.Get(Workspace, "call-1"));
    }

    [Fact]
    public async Task Shutdown_FinalizesCallsStillInProgress()
    {
        // Without this a call left running at shutdown stays in-progress in history forever,
        // because nothing afterwards knows it existed.
        var (service, _, store, _) = await PlacedCallAsync();

        await service.DisposeAsync();

        Assert.NotNull(store.Added[0].EndedAt);
        // Interrupted rather than Failed since ADR-018: the call did not fail, the host went away
        // underneath it, and a deployment must not read as a wave of failed calls.
        Assert.Equal(CallOutcome.Interrupted, store.Added[0].Outcome);
        Assert.Contains(store.OutboxEntries, x => x.EventName == CallEventTypes.Ended);
    }

    [Fact]
    public void OutboxEntry_BacksOffExponentially_AndCapsTheDelay()
    {
        var entry = CallEventOutboxEntry.Pending(
            Guid.NewGuid(), CallEventTypes.Ended, Workspace, "{}", DateTimeOffset.UnixEpoch);
        var baseDelay = TimeSpan.FromSeconds(10);
        var maxDelay = TimeSpan.FromMinutes(15);

        entry.MarkFailed(DateTimeOffset.UnixEpoch, "bus down", baseDelay, maxDelay);
        var firstRetry = entry.NextAttemptAt;

        entry.MarkFailed(DateTimeOffset.UnixEpoch, "bus down", baseDelay, maxDelay);
        var secondRetry = entry.NextAttemptAt;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            entry.MarkFailed(DateTimeOffset.UnixEpoch, "bus down", baseDelay, maxDelay);
        }

        Assert.Equal(DateTimeOffset.UnixEpoch + baseDelay, firstRetry);
        Assert.True(secondRetry > firstRetry);
        // A long-broken consumer is still retried on a predictable cadence.
        Assert.Equal(DateTimeOffset.UnixEpoch + maxDelay, entry.NextAttemptAt);
    }

    [Fact]
    public void CallLog_KeyIsIndependentOfTheProviderCallId()
    {
        // Two channels reporting the same call id must yield two records. While the provider id
        // was the primary key, the second insert collided and the call could not be recorded.
        var first = CallLog.Start(
            "shared-id", Workspace, "ch-1", CallDirection.Outbound,
            "+49301234567", "line", null, null, DateTimeOffset.UnixEpoch);
        var second = CallLog.Start(
            "shared-id", Workspace, "ch-2", CallDirection.Outbound,
            "+49301234568", "line", null, null, DateTimeOffset.UnixEpoch);

        Assert.NotEqual(first.RecordId, second.RecordId);
        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(Guid.Empty, first.RecordId);
    }

    [Fact]
    public void OutboxEntry_DeliveryIsIdempotent()
    {
        var entry = CallEventOutboxEntry.Pending(
            Guid.NewGuid(), CallEventTypes.Ended, Workspace, "{}", DateTimeOffset.UnixEpoch);

        entry.MarkDelivered(DateTimeOffset.UnixEpoch.AddSeconds(1));
        var deliveredAt = entry.DeliveredAt;
        entry.MarkDelivered(DateTimeOffset.UnixEpoch.AddSeconds(2));

        Assert.Equal(deliveredAt, entry.DeliveredAt);
    }

    [Fact]
    public async Task AnEndedCall_TearsDownItsMediaStreams()
    {
        // The media surface has no other signal that the conversation is over (#114): the socket
        // would otherwise stay open and the unspent ticket redeemable.
        var registry = new CommunicationChannelRegistry();
        var mediaStreams = new RecordingMediaStreamTerminator();
        await using var service = new CallControlService(
            registry,
            new RecordingCallLogStore(),
            NullLogger<CallControlService>.Instance,
            TimeProvider.System,
            mediaStreams);

        var call = new ControllableCall("call-1");
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        Assert.Empty(mediaStreams.Closed);
        call.Transition(CallState.Terminated);

        Assert.Equal([(Workspace, "call-1")], mediaStreams.Closed);
    }

    [Fact]
    public async Task ShutdownWhileACallIsLive_AlsoTearsDownItsMediaStreams()
    {
        // Finalization at shutdown ends the call; a socket surviving the host that owns it would
        // never be closed by anything else.
        var registry = new CommunicationChannelRegistry();
        var mediaStreams = new RecordingMediaStreamTerminator();
        var service = new CallControlService(
            registry,
            new RecordingCallLogStore(),
            NullLogger<CallControlService>.Instance,
            TimeProvider.System,
            mediaStreams);

        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = new ControllableCall("call-1") });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        await service.DisposeAsync();

        Assert.Equal([(Workspace, "call-1")], mediaStreams.Closed);
    }

    private static async Task<(CallControlService Service, CommunicationChannelRegistry Registry, RecordingCallLogStore Store, ControllableCall Call)>
        PlacedCallAsync()
    {
        var registry = new CommunicationChannelRegistry();
        var store = new RecordingCallLogStore();
        var service = new CallControlService(
            registry, store, NullLogger<CallControlService>.Instance, TimeProvider.System);

        var call = new ControllableCall("call-1");
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        return (service, registry, store, call);
    }
}

/// <summary>Records which calls had their media streams torn down.</summary>
internal sealed class RecordingMediaStreamTerminator : ICallMediaStreamTerminator
{
    public List<(string WorkspaceKey, string CallId)> Closed { get; } = [];

    public Task CloseForCallAsync(string workspaceKey, string callId, CancellationToken cancellationToken = default)
    {
        Closed.Add((workspaceKey, callId));
        return Task.CompletedTask;
    }
}
