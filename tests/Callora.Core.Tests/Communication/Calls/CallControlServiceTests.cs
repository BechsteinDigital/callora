using Callora.Core.Tests.Support;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Domain.Calls;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Communication.Calls;

/// <summary>
/// The channel-neutral call-control primitive: places calls through the workspace's registered voice
/// channel, tracks them via <see cref="ICall"/>, records <see cref="CallLog"/> history and publishes
/// <c>call.*</c> business events on each transition — workspace-scoped, with no dialer/PBX behaviour.
/// </summary>
public sealed class CallControlServiceTests
{
    private const string Workspace = "ws-a";

    [Fact]
    public async Task PlaceCall_WithoutVoiceChannel_Throws()
    {
        var (service, _, _, _) = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567")));
    }

    [Fact]
    public async Task PlaceCall_RecordsStartLog_AndPublishesPlaced()
    {
        var (service, registry, store, bus) = CreateService();
        var call = new ControllableCall("call-1");
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });

        var snapshot = await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        Assert.Equal("call-1", snapshot.CallId);
        Assert.Equal(CallState.Connecting, snapshot.State);
        Assert.Equal(CallDirection.Outbound, snapshot.Direction);
        Assert.Equal("+49301234567", snapshot.Target);

        var log = Assert.Single(store.Added);
        Assert.Equal(CallDirection.Outbound, log.Direction);
        Assert.Equal(CallOutcome.InProgress, log.Outcome);
        Assert.Null(log.AnsweredAt);
        Assert.Null(log.EndedAt);

        var published = Assert.Single(bus.Published);
        Assert.Equal(CallEventTypes.Placed, published.EventName);
        Assert.Equal(Workspace, published.WorkspaceKey);
    }

    [Fact]
    public async Task PlaceCall_LogsVerbatimOperatorTarget_NotTheChannelReportedTarget()
    {
        var (service, registry, store, _) = CreateService();
        // The channel/SDK may report a normalized remote party; the history + call.placed must keep the
        // operator's verbatim "to", while the live snapshot reflects the call's actual target.
        var call = new ControllableCall("call-1", target: "sip:+49301234567@sbc.example.com");
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });

        var snapshot = await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        Assert.Equal("+49301234567", store.Added[0].RemoteParty);
        Assert.Equal("sip:+49301234567@sbc.example.com", snapshot.Target);
    }

    [Fact]
    public async Task PlaceCall_WithExplicitChannelId_UsesThatChannel()
    {
        var (service, registry, _, _) = CreateService();
        var chOne = new FakeCommunicationChannel { ChannelId = "ch-1", NextCall = new ControllableCall("a") };
        var chTwo = new FakeCommunicationChannel { ChannelId = "ch-2", NextCall = new ControllableCall("b") };
        registry.Register(Workspace, chOne);
        registry.Register(Workspace, chTwo);

        var snapshot = await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567", ChannelId: "ch-2"));

        Assert.Equal("b", snapshot.CallId);
        Assert.NotNull(chTwo.PlacedTarget);
        Assert.Null(chOne.PlacedTarget);
    }

    [Fact]
    public async Task PlaceCall_WithUnknownChannelId_Throws()
    {
        var (service, registry, _, _) = CreateService();
        registry.Register(Workspace, new FakeCommunicationChannel { ChannelId = "ch-1", NextCall = new ControllableCall("a") });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567", ChannelId: "does-not-exist")));
    }

    [Fact]
    public async Task Connected_MarksAnswered_AndPublishesStateChanged()
    {
        var (service, registry, store, bus) = CreateService();
        var call = new ControllableCall("call-1");
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        call.Transition(CallState.Connected);

        Assert.NotNull(store.Added[0].AnsweredAt);
        Assert.Contains(bus.Published, e => e.EventName == CallEventTypes.StateChanged);
    }

    [Fact]
    public async Task Terminated_AfterAnswer_EndsCompleted_PublishesEnded_AndUntracks()
    {
        var (service, registry, store, bus) = CreateService();
        var call = new ControllableCall("call-1");
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        call.Transition(CallState.Connected);
        call.Transition(CallState.Terminated);

        Assert.Equal(CallOutcome.Completed, store.Added[0].Outcome);
        Assert.NotNull(store.Added[0].EndedAt);
        Assert.Contains(bus.Published, e => e.EventName == CallEventTypes.Ended);
        Assert.Null(service.Get(Workspace, "call-1")); // finalized calls are no longer tracked
    }

    [Fact]
    public async Task Terminated_WithoutAnswer_EndsFailed()
    {
        var (service, registry, store, _) = CreateService();
        var call = new ControllableCall("call-1");
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        call.Transition(CallState.Terminated);

        Assert.Equal(CallOutcome.Failed, store.Added[0].Outcome);
        Assert.NotNull(store.Added[0].EndedAt);
    }

    // --- Termination reason → outcome + disconnect cause (SDK-enriched) ---

    [Fact]
    public async Task Terminated_Unanswered_Busy_EndsBusy_WithSipDisconnectCause()
    {
        var (service, registry, store, _) = CreateService();
        var call = new ControllableCall("call-1")
        {
            TerminationReason = Reason(CallTerminationCategory.Busy, sipStatusCode: 486, reasonPhrase: "Busy Here"),
        };
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        call.Transition(CallState.Terminated);

        Assert.Equal(CallOutcome.Busy, store.Added[0].Outcome);
        Assert.Contains("486", store.Added[0].DisconnectCause);
        Assert.Contains("Busy Here", store.Added[0].DisconnectCause);
    }

    [Fact]
    public async Task Terminated_Unanswered_NoAnswer_EndsNoAnswer()
    {
        var (service, registry, store, _) = CreateService();
        var call = new ControllableCall("call-1")
        {
            TerminationReason = Reason(CallTerminationCategory.NoAnswer, sipStatusCode: 408, reasonPhrase: "Request Timeout"),
        };
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        call.Transition(CallState.Terminated);

        Assert.Equal(CallOutcome.NoAnswer, store.Added[0].Outcome);
    }

    [Fact]
    public async Task Terminated_UnansweredInbound_Rejected_EndsRejected()
    {
        var (service, _, store, _) = CreateService();
        var call = new ControllableCall("in-1", initial: CallState.Ringing, direction: CallDirection.Inbound)
        {
            TerminationReason = Reason(CallTerminationCategory.Rejected, sipStatusCode: 603, reasonPhrase: "Decline"),
        };
        await service.ObserveIncomingAsync(Workspace, new FakeCommunicationChannel(), call);

        call.Transition(CallState.Terminated);

        Assert.Equal(CallOutcome.Rejected, store.Added[0].Outcome);
    }

    [Fact]
    public async Task Terminated_Unanswered_Canceled_EndsCanceled()
    {
        var (service, registry, store, _) = CreateService();
        var call = new ControllableCall("call-1")
        {
            TerminationReason = Reason(CallTerminationCategory.Canceled, sipStatusCode: 487, reasonPhrase: "Request Terminated"),
        };
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        call.Transition(CallState.Terminated);

        Assert.Equal(CallOutcome.Canceled, store.Added[0].Outcome);
    }

    [Fact]
    public async Task Terminated_Answered_Completed_EndsCompleted_WithDisconnectCause()
    {
        var (service, registry, store, _) = CreateService();
        var call = new ControllableCall("call-1")
        {
            TerminationReason = Reason(CallTerminationCategory.Completed, sipStatusCode: null, reasonPhrase: "Normal Clearing"),
        };
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        call.Transition(CallState.Connected);
        call.Transition(CallState.Terminated);

        Assert.Equal(CallOutcome.Completed, store.Added[0].Outcome);
        Assert.Equal("Normal Clearing", store.Added[0].DisconnectCause);
    }

    [Fact]
    public async Task Terminated_Answered_Failed_EndsFailed()
    {
        var (service, registry, store, _) = CreateService();
        var call = new ControllableCall("call-1")
        {
            TerminationReason = Reason(CallTerminationCategory.Failed, sipStatusCode: 500, reasonPhrase: "Server Internal Error"),
        };
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        call.Transition(CallState.Connected);
        call.Transition(CallState.Terminated);

        // An answered call reconciles any non-Completed reason to Failed (CallLog forbids Busy/etc. once answered).
        Assert.Equal(CallOutcome.Failed, store.Added[0].Outcome);
    }

    [Fact]
    public async Task Terminated_Answered_ReasonBusy_ReconciledToFailed()
    {
        var (service, registry, store, _) = CreateService();
        // An answered call that reports an unanswered-style category is a protocol anomaly → Failed, never Busy.
        var call = new ControllableCall("call-1")
        {
            TerminationReason = Reason(CallTerminationCategory.Busy, sipStatusCode: 486, reasonPhrase: "Busy Here"),
        };
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        call.Transition(CallState.Connected);
        call.Transition(CallState.Terminated);

        Assert.Equal(CallOutcome.Failed, store.Added[0].Outcome);
    }

    // --- Fallback: no reason (no SDK / no cause) keeps the pre-enrichment heuristic ---

    [Fact]
    public async Task Terminated_NoReason_UnansweredInbound_FallsBackToMissed()
    {
        var (service, _, store, _) = CreateService();
        var call = new ControllableCall("in-1", initial: CallState.Ringing, direction: CallDirection.Inbound);
        await service.ObserveIncomingAsync(Workspace, new FakeCommunicationChannel(), call);

        call.Transition(CallState.Terminated);

        Assert.Equal(CallOutcome.Missed, store.Added[0].Outcome);
        Assert.Null(store.Added[0].DisconnectCause);
    }

    [Fact]
    public async Task Terminated_NoReason_UnansweredOutbound_FallsBackToFailed()
    {
        var (service, registry, store, _) = CreateService();
        var call = new ControllableCall("call-1");
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        call.Transition(CallState.Terminated);

        Assert.Equal(CallOutcome.Failed, store.Added[0].Outcome);
        Assert.Null(store.Added[0].DisconnectCause);
    }

    [Fact]
    public async Task Terminated_NoReason_Answered_FallsBackToCompleted()
    {
        var (service, registry, store, _) = CreateService();
        var call = new ControllableCall("call-1");
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        call.Transition(CallState.Connected);
        call.Transition(CallState.Terminated);

        Assert.Equal(CallOutcome.Completed, store.Added[0].Outcome);
        Assert.Null(store.Added[0].DisconnectCause);
    }

    private static CallTerminationReason Reason(
        CallTerminationCategory category, int? sipStatusCode, string? reasonPhrase) =>
        new(category, sipStatusCode, reasonPhrase, CallTerminatedBy.Remote, RetryAfterSeconds: null);

    [Fact]
    public async Task PlaceCall_WhenCallAlreadyConnected_RecordsAnsweredViaRaceRecheck()
    {
        var (service, registry, store, _) = CreateService();
        var call = new ControllableCall("call-1", initial: CallState.Connected);
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });

        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        Assert.NotNull(store.Added[0].AnsweredAt); // no missed transition despite connecting before we subscribed
    }

    [Fact]
    public async Task Hangup_TrackedCall_EndsCall_AndReturnsTrue()
    {
        var (service, registry, store, _) = CreateService();
        var call = new ControllableCall("call-1");
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        var result = await service.HangupAsync(Workspace, "call-1");

        Assert.True(result);
        Assert.True(call.HangupCalled);
        Assert.NotNull(store.Added[0].EndedAt);
        Assert.Null(service.Get(Workspace, "call-1"));
    }

    [Fact]
    public async Task Hangup_UnknownCall_ReturnsFalse()
    {
        var (service, _, _, _) = CreateService();

        Assert.False(await service.HangupAsync(Workspace, "unknown"));
    }

    [Fact]
    public async Task Hangup_FromAnotherWorkspace_ReturnsFalse_AndDoesNotTouchCall()
    {
        var (service, registry, _, _) = CreateService();
        var call = new ControllableCall("call-1");
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        var result = await service.HangupAsync("other-ws", "call-1");

        Assert.False(result);
        Assert.False(call.HangupCalled); // workspace scoping protects another workspace's call
    }

    [Fact]
    public async Task Get_IsWorkspaceScoped()
    {
        var (service, registry, _, _) = CreateService();
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = new ControllableCall("call-1") });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        Assert.NotNull(service.Get(Workspace, "call-1"));
        Assert.Null(service.Get("other-ws", "call-1"));
    }

    [Fact]
    public async Task Lifecycle_WithoutEventBus_DoesNotThrow()
    {
        var registry = new CommunicationChannelRegistry();
        var store = new RecordingCallLogStore();
        var service = new CallControlService(
            registry, store, eventBus: null, NullLogger<CallControlService>.Instance, TimeProvider.System);
        var call = new ControllableCall("call-1");
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });

        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));
        call.Transition(CallState.Connected);
        call.Transition(CallState.Terminated);

        Assert.Equal(CallOutcome.Completed, store.Added[0].Outcome); // history still recorded without a bus
    }

    // --- Inbound observation (no auto-answer / no routing) ---

    [Fact]
    public async Task ObserveIncoming_RecordsRingingLog_AndPublishesRinging()
    {
        var (service, _, store, bus) = CreateService();
        var channel = new FakeCommunicationChannel();
        var call = new ControllableCall("in-1", initial: CallState.Ringing, direction: CallDirection.Inbound);

        await service.ObserveIncomingAsync(Workspace, channel, call);

        var log = Assert.Single(store.Added);
        Assert.Equal(CallDirection.Inbound, log.Direction);
        Assert.Equal(CallOutcome.InProgress, log.Outcome);
        Assert.Contains(bus.Published, e => e.EventName == CallEventTypes.Ringing);
        Assert.NotNull(service.Get(Workspace, "in-1")); // tracked until it ends
    }

    [Fact]
    public async Task ObserveIncoming_Answered_MarksAnswered()
    {
        var (service, _, store, _) = CreateService();
        var call = new ControllableCall("in-1", initial: CallState.Ringing, direction: CallDirection.Inbound);
        await service.ObserveIncomingAsync(Workspace, new FakeCommunicationChannel(), call);

        call.Transition(CallState.Connected);

        Assert.NotNull(store.Added[0].AnsweredAt);
    }

    [Fact]
    public async Task ObserveIncoming_UnansweredThenEnded_RecordsMissed()
    {
        var (service, _, store, _) = CreateService();
        var call = new ControllableCall("in-1", initial: CallState.Ringing, direction: CallDirection.Inbound);
        await service.ObserveIncomingAsync(Workspace, new FakeCommunicationChannel(), call);

        call.Transition(CallState.Terminated);

        Assert.Equal(CallOutcome.Missed, store.Added[0].Outcome); // inbound unanswered = missed, not failed
    }

    [Fact]
    public async Task ObserveIncoming_AnsweredThenEnded_RecordsCompleted()
    {
        var (service, _, store, _) = CreateService();
        var call = new ControllableCall("in-1", initial: CallState.Ringing, direction: CallDirection.Inbound);
        await service.ObserveIncomingAsync(Workspace, new FakeCommunicationChannel(), call);

        call.Transition(CallState.Connected);
        call.Transition(CallState.Terminated);

        Assert.Equal(CallOutcome.Completed, store.Added[0].Outcome);
    }

    [Fact]
    public async Task Terminated_FiredTwice_FinalizesExactlyOnce()
    {
        var (service, registry, store, _) = CreateService();
        var call = new ControllableCall("call-1");
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));

        call.Transition(CallState.Terminated);
        call.Transition(CallState.Terminated); // a duplicate terminal transition must be a no-op

        Assert.Equal(1, store.UpdateCount); // finalized once, no second End (which CallLog would reject)
        Assert.Equal(CallOutcome.Failed, store.Added[0].Outcome);
    }

    [Fact]
    public async Task Hangup_AfterCallAlreadyEnded_ReturnsFalse()
    {
        var (service, registry, _, _) = CreateService();
        var call = new ControllableCall("call-1");
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });
        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));
        call.Transition(CallState.Terminated); // ends and untracks

        Assert.False(await service.HangupAsync(Workspace, "call-1"));
    }

    private static (CallControlService Service, CommunicationChannelRegistry Registry, RecordingCallLogStore Store, RecordingBusinessEventBus Bus)
        CreateService()
    {
        var registry = new CommunicationChannelRegistry();
        var store = new RecordingCallLogStore();
        var bus = new RecordingBusinessEventBus();
        var service = new CallControlService(
            registry, store, bus, NullLogger<CallControlService>.Instance, TimeProvider.System);
        return (service, registry, store, bus);
    }
}

/// <summary>A controllable <see cref="ICommunicationChannel"/> that hands out a preset call on place.</summary>
internal sealed class FakeCommunicationChannel : ICommunicationChannel
{
    public string ChannelId { get; init; } = "ch-1";

    public string DisplayName { get; init; } = "Fake Channel";

    public string PluginId { get; init; } = "communication";

    public IReadOnlyCollection<string> Capabilities { get; init; } = [CommunicationCapabilities.Voice];

    public ChannelHealth Health => ChannelHealth.Up;

    public ICall? NextCall { get; set; }

    public CallTarget? PlacedTarget { get; private set; }

    public event EventHandler<ChannelHealthChangedEventArgs>? HealthChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<IncomingCallEventArgs>? IncomingCall;

    public void RaiseIncoming(ICall call) => IncomingCall?.Invoke(this, new IncomingCallEventArgs(call));

    public bool HasIncomingSubscribers => IncomingCall is not null;

    public Task<ICall> PlaceCallAsync(CallTarget target, CancellationToken cancellationToken = default)
    {
        PlacedTarget = target;
        return Task.FromResult(NextCall ?? throw new InvalidOperationException("NextCall not set."));
    }
}

/// <summary>A driveable <see cref="ICall"/>: tests advance its state to fire the lifecycle handlers.</summary>
internal sealed class ControllableCall : ICall
{
    public ControllableCall(
        string callId,
        CallState initial = CallState.Connecting,
        CallDirection direction = CallDirection.Outbound,
        string target = "+49301234567")
    {
        CallId = callId;
        State = initial;
        Direction = direction;
        Target = new CallTarget(target);
    }

    public string CallId { get; }

    public CallState State { get; private set; }

    public CallDirection Direction { get; }

    public CallTarget Target { get; }

    public CallTerminationReason? TerminationReason { get; set; }

    public bool HangupCalled { get; private set; }

    public event EventHandler<CallStateChangedEventArgs>? StateChanged;

    public void Transition(CallState next)
    {
        var previous = State;
        State = next;
        StateChanged?.Invoke(this, new CallStateChangedEventArgs(previous, next));
    }

    public Task AcceptAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RejectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task HangupAsync(CancellationToken cancellationToken = default)
    {
        HangupCalled = true;
        Transition(CallState.Terminated);
        return Task.CompletedTask;
    }

    public Task SendDtmfAsync(char tone, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>Records call-log writes in memory; the stored <see cref="CallLog"/> is the live mutated object.</summary>
internal sealed class RecordingCallLogStore : ICallLogStore
{
    public List<CallLog> Added { get; } = [];

    public int UpdateCount { get; private set; }

    public Task AddAsync(CallLog log, CancellationToken cancellationToken = default)
    {
        Added.Add(log);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CallLog log, CancellationToken cancellationToken = default)
    {
        UpdateCount++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CallLog>> ListRecentAsync(string workspaceKey, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CallLog>>(Added);

    public Task<int> DeleteByWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}
