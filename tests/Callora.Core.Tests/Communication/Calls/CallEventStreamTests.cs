using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Api.WebSocket;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Domain.Calls;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Communication.Calls;

/// <summary>
/// The live path a dialer follows (#116). The outbox delivers durably on a job cadence, which is
/// right for a webhook and far too slow for a UI that has to light up while the phone is ringing.
/// </summary>
public sealed class CallEventStreamTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ASubscriberSeesItsWorkspacesTransitions()
    {
        var broadcaster = new CallEventBroadcaster();
        using var subscription = broadcaster.Subscribe("ws-a");
        var registry = new CommunicationChannelRegistry();
        await using var service = NewService(registry, broadcaster);
        registry.Register("ws-a", new FakeCommunicationChannel { NextCall = new ControllableCall("call-1") });

        await service.PlaceCallAsync(new PlaceCallCommand("ws-a", "+49301234567"));

        Assert.True(subscription.Events.TryRead(out var notification));
        Assert.Equal(CallEventTypes.Placed, notification!.EventName);
        Assert.Equal("call-1", notification.CallId);
        Assert.Equal("Outbound", notification.Direction);
    }

    [Fact]
    public async Task ASubscriberNeverSeesAnotherWorkspacesTransitions()
    {
        var broadcaster = new CallEventBroadcaster();
        using var subscription = broadcaster.Subscribe("ws-b");
        var registry = new CommunicationChannelRegistry();
        await using var service = NewService(registry, broadcaster);
        registry.Register("ws-a", new FakeCommunicationChannel { NextCall = new ControllableCall("call-1") });

        await service.PlaceCallAsync(new PlaceCallCommand("ws-a", "+49301234567"));

        Assert.False(subscription.Events.TryRead(out _));
    }

    [Fact]
    public async Task EveryLifecycleTransitionReachesTheStream()
    {
        var broadcaster = new CallEventBroadcaster();
        using var subscription = broadcaster.Subscribe("ws-a");
        var registry = new CommunicationChannelRegistry();
        await using var service = NewService(registry, broadcaster);
        var call = new ControllableCall("call-1");
        registry.Register("ws-a", new FakeCommunicationChannel { NextCall = call });

        await service.PlaceCallAsync(new PlaceCallCommand("ws-a", "+49301234567"));
        call.Transition(CallState.Connected);
        call.Transition(CallState.Terminated);

        var seen = new List<string>();
        while (subscription.Events.TryRead(out var notification))
        {
            seen.Add(notification.EventName);
        }

        Assert.Equal([CallEventTypes.Placed, CallEventTypes.StateChanged, CallEventTypes.Ended], seen);
    }

    [Fact]
    public void ASlowSubscriberLosesItsOldestEvents_RatherThanBlockingTheCall()
    {
        // A stalled browser tab must never be able to slow a conversation down. The current state is
        // always one GET calls/active away, so a gap costs a refresh, not correctness.
        var broadcaster = new CallEventBroadcaster();
        using var subscription = broadcaster.Subscribe("ws-a");

        for (var index = 0; index < CallEventBroadcaster.SubscriberQueueCapacity + 10; index++)
        {
            broadcaster.Publish(Notification($"call-{index}"));
        }

        var received = new List<string>();
        while (subscription.Events.TryRead(out var notification))
        {
            received.Add(notification.CallId);
        }

        Assert.Equal(CallEventBroadcaster.SubscriberQueueCapacity, received.Count);
        Assert.Equal("call-10", received[0]); // the oldest ten were dropped
    }

    [Fact]
    public void DisposingASubscriptionUnsubscribesAndCompletesIt()
    {
        var broadcaster = new CallEventBroadcaster();
        var subscription = broadcaster.Subscribe("ws-a");
        Assert.Equal(1, broadcaster.SubscriberCount);

        subscription.Dispose();
        broadcaster.Publish(Notification("call-1"));

        Assert.Equal(0, broadcaster.SubscriberCount);
        Assert.False(subscription.Events.TryRead(out _));
        Assert.True(subscription.Events.Completion.IsCompleted);
    }

    [Fact]
    public void PublishingWithNoSubscribersIsHarmless()
    {
        var broadcaster = new CallEventBroadcaster();

        broadcaster.Publish(Notification("call-1"));

        Assert.Equal(0, broadcaster.SubscriberCount);
    }

    // --- tickets ---

    [Fact]
    public void ATicketOpensExactlyOneSocket()
    {
        var tickets = new CallEventTicketStore(new FakeTimeProvider(Now), TimeSpan.FromMinutes(2));
        var token = tickets.Mint("ws-a");

        Assert.Equal("ws-a", tickets.TryConsume(token));
        Assert.Null(tickets.TryConsume(token));
    }

    [Fact]
    public void AnExpiredTicketIsRefused()
    {
        var clock = new FakeTimeProvider(Now);
        var tickets = new CallEventTicketStore(clock, TimeSpan.FromMinutes(2));
        var token = tickets.Mint("ws-a");

        clock.Advance(TimeSpan.FromMinutes(3));

        Assert.Null(tickets.TryConsume(token));
    }

    [Fact]
    public void AnUnknownTicketIsRefused()
    {
        var tickets = new CallEventTicketStore(new FakeTimeProvider(Now), TimeSpan.FromMinutes(2));

        Assert.Null(tickets.TryConsume("not-a-token"));
        Assert.Null(tickets.TryConsume(""));
    }

    [Fact]
    public void EveryMintProducesAFreshToken()
    {
        var tickets = new CallEventTicketStore(new FakeTimeProvider(Now), TimeSpan.FromMinutes(2));

        Assert.NotEqual(tickets.Mint("ws-a"), tickets.Mint("ws-a"));
    }

    [Fact]
    public void TicketsMintedAndNeverRedeemedAreSweptAway()
    {
        // Otherwise a client that mints on every page load grows the dictionary forever.
        var clock = new FakeTimeProvider(Now);
        var tickets = new CallEventTicketStore(clock, TimeSpan.FromMinutes(2));
        var abandoned = tickets.Mint("ws-a");

        clock.Advance(TimeSpan.FromMinutes(5));
        tickets.Mint("ws-a"); // the sweep runs at mint time

        Assert.Null(tickets.TryConsume(abandoned));
    }

    private static CallEventNotification Notification(string callId) =>
        CallEventNotification.For(
            CallEventTypes.Ringing, "ws-a", callId, CallDirection.Inbound, CallState.Ringing, "+49301234567", Now);

    private static CallControlService NewService(
        CommunicationChannelRegistry registry, CallEventBroadcaster broadcaster) =>
        new(registry,
            new RecordingCallLogStore(),
            NullLogger<CallControlService>.Instance,
            TimeProvider.System,
            mediaStreams: null,
            liveEvents: broadcaster);
}
