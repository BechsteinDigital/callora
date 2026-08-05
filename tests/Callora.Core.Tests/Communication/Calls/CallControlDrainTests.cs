using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Domain.Calls;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Communication.Calls;

/// <summary>
/// What a drain waits for (ADR-018 §2.1), and what happens to whatever outlives it. Stopping
/// mid-conversation is the failure mode this exists to avoid; recording that stop as a call failure
/// is the one it avoids second.
/// </summary>
public sealed class CallControlDrainTests
{
    private const string Workspace = "ws-a";

    [Fact]
    public async Task WaitingWithNothingInFlightReturnsImmediately()
    {
        await using var service = NewService(new CommunicationChannelRegistry(), new RecordingCallLogStore());

        // Not a timing assertion: a wait that needed a call to end would hang here forever.
        await service.WaitForDrainAsync(GuardToken());

        Assert.Equal(0, service.ActiveCallCount);
    }

    [Fact]
    public async Task WaitingCompletesWhenTheLastCallEnds()
    {
        var (service, _, call) = await RingingInboundAsync();
        Assert.Equal(1, service.ActiveCallCount);

        var waiting = service.WaitForDrainAsync(GuardToken());
        Assert.False(waiting.IsCompleted);

        call.Transition(CallState.Terminated);

        await waiting;
        Assert.Equal(0, service.ActiveCallCount);
    }

    [Fact]
    public async Task ACallThatEndsBeforeTheWaitStartsDoesNotStrandIt()
    {
        var (service, _, call) = await RingingInboundAsync();
        call.Transition(CallState.Terminated);

        // The race the signal has to survive: the event that would complete the wait already
        // happened when the wait begins.
        await service.WaitForDrainAsync(GuardToken());
    }

    [Fact]
    public async Task AnExpiredDeadlineSurfacesAsCancellation()
    {
        var (service, _, _) = await RingingInboundAsync();
        using var expired = new CancellationTokenSource();
        await expired.CancelAsync();

        // The host reads this as "did not finish draining in time" and stops the plugin anyway.
        await Assert.ThrowsAsync<TaskCanceledException>(() => service.WaitForDrainAsync(expired.Token));
    }

    [Fact]
    public async Task ACallStillUpAtDisposalIsRecordedAsInterruptedRatherThanFailed()
    {
        var (service, store, _) = await RingingInboundAsync();

        await service.DisposeAsync();

        // Failed would tell an operator reading the history that something went wrong with the call.
        // Nothing did; the host went away underneath it.
        Assert.Equal(CallOutcome.Interrupted, store.Added[0].Outcome);
    }

    /// <summary>
    /// A wait that should complete would hang the suite forever if the signal broke, so every wait
    /// here carries a deadline that turns "hangs" into "fails".
    /// </summary>
    private static CancellationToken GuardToken() =>
        new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;

    private static CallControlService NewService(CommunicationChannelRegistry registry, RecordingCallLogStore store) =>
        new(registry, store, NullLogger<CallControlService>.Instance, TimeProvider.System);

    private static async Task<(CallControlService Service, RecordingCallLogStore Store, ControllableCall Call)>
        RingingInboundAsync()
    {
        var registry = new CommunicationChannelRegistry();
        var store = new RecordingCallLogStore();
        var service = NewService(registry, store);
        var channel = new FakeCommunicationChannel { ChannelId = "ch-1" };
        var call = new ControllableCall("call-1", CallState.Ringing, CallDirection.Inbound, "+49301234567");
        registry.Register(Workspace, channel);

        await service.ObserveIncomingAsync(Workspace, channel, call);
        return (service, store, call);
    }
}
