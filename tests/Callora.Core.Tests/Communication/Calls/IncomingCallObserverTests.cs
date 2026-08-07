using System.Linq;
using Callora.Core.Tests.Support;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Communication.Calls;

/// <summary>
/// The inbound-call observer attaches to every registered channel (present and future) and feeds each
/// incoming call to the call-control primitive — recording history and <c>call.ringing</c> without
/// answering or routing. It detaches cleanly on unregister and dispose.
/// </summary>
public sealed class IncomingCallObserverTests
{
    private const string Workspace = "ws-a";

    [Fact]
    public void ChannelRegisteredAfterStart_IncomingCall_IsObserved()
    {
        var (observer, registry, store) = Create();
        observer.Start();
        var channel = new FakeCommunicationChannel();
        registry.Register(Workspace, channel);

        channel.RaiseIncoming(Inbound());

        Assert.Single(store.Added);
    }

    [Fact]
    public void ChannelRegisteredBeforeStart_IsObserved()
    {
        // GetAllRegistrations lets the observer attach to channels that registered before it started.
        var (observer, registry, store) = Create();
        var channel = new FakeCommunicationChannel();
        registry.Register(Workspace, channel);
        observer.Start();

        channel.RaiseIncoming(Inbound());

        Assert.Single(store.Added);
    }

    [Fact]
    public void UnregisteredChannel_IsNoLongerObserved()
    {
        var (observer, registry, store) = Create();
        observer.Start();
        var channel = new FakeCommunicationChannel();
        var handle = registry.Register(Workspace, channel);

        handle.Dispose(); // fires ChannelUnregistered → observer detaches
        channel.RaiseIncoming(Inbound());

        Assert.Empty(store.Added);
        Assert.False(channel.HasIncomingSubscribers);
    }

    [Fact]
    public void Dispose_DetachesFromChannels()
    {
        var (observer, registry, store) = Create();
        observer.Start();
        var channel = new FakeCommunicationChannel();
        registry.Register(Workspace, channel);

        observer.Dispose();
        channel.RaiseIncoming(Inbound());

        Assert.Empty(store.Added);
        Assert.False(channel.HasIncomingSubscribers);
    }

    private static (IncomingCallObserver Observer, CommunicationChannelRegistry Registry, RecordingCallLogStore Store) Create(
        IncomingCallOwnerRegistry? owners = null,
        CallJourney? journey = null)
    {
        var registry = new CommunicationChannelRegistry();
        var store = new RecordingCallLogStore();
        var service = new CallControlService(
            registry, store, NullLogger<CallControlService>.Instance, TimeProvider.System,
            journey: journey);
        return (new IncomingCallObserver(registry, service, owners, journey: journey), registry, store);
    }

    // ── The record of what happened ─────────────────────────────────────────

    [Fact]
    public async Task AnArrivingCall_IsWrittenIntoItsJourney()
    {
        // The one question a call history cannot answer today: what happened, and to whom did it go.
        var journey = new CallJourney();
        var (observer, registry, _) = Create(journey: journey);
        observer.Start();
        var channel = new FakeCommunicationChannel();
        registry.Register(Workspace, channel);

        channel.RaiseIncoming(Inbound());
        await Task.Delay(50);

        Assert.Contains("call.ringing", journey.Read(Workspace, "in-1").Select(step => step.Step));
    }

    [Fact]
    public async Task AClaimedCall_NamesWhoTookIt()
    {
        var journey = new CallJourney();
        var owners = new IncomingCallOwnerRegistry();
        owners.Register(Workspace, new NamedOwner("videoconference", "Telefon-Einwahl"));
        var (observer, registry, _) = Create(owners: owners, journey: journey);
        observer.Start();
        var channel = new FakeCommunicationChannel();
        registry.Register(Workspace, channel);

        channel.RaiseIncoming(Inbound());
        await Task.Delay(50);

        var claimed = Assert.Single(journey.Read(Workspace, "in-1"), step => step.Step == "call.claimed");
        Assert.Contains("Telefon-Einwahl", claimed.Detail);
    }

    [Fact]
    public async Task ACallNobodyWanted_SaysSoInsteadOfJustEnding()
    {
        // "Rejected" on its own reads like a fault. That nobody claimed it is the actual reason, and
        // it is the one an operator can act on — by configuring the number.
        var journey = new CallJourney();
        var owners = new IncomingCallOwnerRegistry();
        owners.Register(Workspace, new DecliningOwner());
        var (observer, registry, store) = Create(owners: owners, journey: journey);
        observer.Start();
        var channel = new FakeCommunicationChannel();
        registry.Register(Workspace, channel);

        channel.RaiseIncoming(Inbound());
        await Task.Delay(50);

        // On the history record, not in the buffer: rejecting ends the call, and the buffer hands the
        // story over the moment it does. For a call nobody wanted, history is the only place there is.
        Assert.Contains("call.unclaimed", store.Added[0].Journey.Select(step => step.Step));
    }

    [Fact]
    public async Task WhenTheCallEnds_ItsJourneyMovesOntoItsHistoryRecord()
    {
        // The buffer is for the running call; afterwards the story has to be where an operator looks.
        var journey = new CallJourney();
        var (observer, registry, store) = Create(journey: journey);
        observer.Start();
        var channel = new FakeCommunicationChannel();
        registry.Register(Workspace, channel);
        var call = Inbound();

        channel.RaiseIncoming(call);
        await Task.Delay(50);
        call.Transition(CallState.Terminated);
        await Task.Delay(50);

        Assert.Contains("call.ringing", store.Added[0].Journey.Select(step => step.Step));
        Assert.Empty(journey.Read(Workspace, "in-1"));
    }

    private static ControllableCall Inbound(string id = "in-1") =>
        new(id, initial: CallState.Ringing, direction: CallDirection.Inbound);
}
