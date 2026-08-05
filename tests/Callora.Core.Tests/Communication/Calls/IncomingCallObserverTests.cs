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

    private static (IncomingCallObserver Observer, CommunicationChannelRegistry Registry, RecordingCallLogStore Store) Create()
    {
        var registry = new CommunicationChannelRegistry();
        var store = new RecordingCallLogStore();
        var service = new CallControlService(
            registry, store, NullLogger<CallControlService>.Instance, TimeProvider.System);
        return (new IncomingCallObserver(registry, service), registry, store);
    }

    private static ControllableCall Inbound(string id = "in-1") =>
        new(id, initial: CallState.Ringing, direction: CallDirection.Inbound);
}
