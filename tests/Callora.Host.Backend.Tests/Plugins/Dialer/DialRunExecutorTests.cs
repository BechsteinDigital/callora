using Callora.Contracts.Communication;
using Callora.Host.Backend.Application.Communication;
using Callora.Host.Backend.Tests.Support;
using Callora.Plugins.Dialer.Application.Numbers;
using Callora.Plugins.Dialer.Application.Runs;
using Xunit;

namespace Callora.Host.Backend.Tests.Plugins.Dialer;

/// <summary>
/// Contract proof: the dialer executes runs over ANY ICommunicationChannel —
/// these tests use the protocol-free fake channel, not SIP.
/// </summary>
public sealed class DialRunExecutorTests
{
    [Fact]
    public async Task Execute_WithoutVoiceChannel_Throws()
    {
        var registry = new CommunicationChannelRegistry();
        var executor = new DialRunExecutor(registry);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(
                "workspace-a",
                [NewNumber("+4930111")],
                DialRunOptions.Default,
                CancellationToken.None));
    }

    [Fact]
    public async Task Execute_DialsAllNumbersSequentially_OverContractChannel()
    {
        var registry = new CommunicationChannelRegistry();
        var channel = new StaticCommunicationChannel("fake-voice");
        registry.Register("workspace-a", channel);
        var executor = new DialRunExecutor(registry);

        var executeTask = executor.ExecuteAsync(
            "workspace-a",
            [NewNumber("+4930111"), NewNumber("+4930222")],
            new DialRunOptions(TimeSpan.FromSeconds(5)),
            CancellationToken.None);

        // Erster Anruf: verbinden, dann beenden.
        var firstCall = await WaitForPlacedCallAsync(channel, index: 0);
        firstCall.TransitionTo(CallState.Connected);
        firstCall.TransitionTo(CallState.Terminated);

        // Zweiter Anruf: endet ohne Verbindung.
        var secondCall = await WaitForPlacedCallAsync(channel, index: 1);
        secondCall.TransitionTo(CallState.Terminated);

        var attempts = await executeTask;

        Assert.Equal(2, attempts.Count);
        Assert.Equal(DialAttemptOutcome.Connected, attempts[0].Outcome);
        Assert.Equal("+4930111", attempts[0].Number);
        Assert.Equal(DialAttemptOutcome.NotConnected, attempts[1].Outcome);
        Assert.Equal("+4930222", attempts[1].Number);
    }

    [Fact]
    public async Task Execute_TimesOutStuckCall_AndHangsUp()
    {
        var registry = new CommunicationChannelRegistry();
        var channel = new StaticCommunicationChannel("fake-voice");
        registry.Register("workspace-a", channel);
        var executor = new DialRunExecutor(registry);

        var attempts = await executor.ExecuteAsync(
            "workspace-a",
            [NewNumber("+4930111")],
            new DialRunOptions(TimeSpan.FromMilliseconds(250)),
            CancellationToken.None);

        var attempt = Assert.Single(attempts);
        Assert.Equal(DialAttemptOutcome.TimedOut, attempt.Outcome);
        Assert.Equal(CallState.Terminated, channel.PlacedCalls[0].State);
    }

    [Fact]
    public async Task Tracker_StartsRun_TracksCompletion_AndRejectsParallelRuns()
    {
        var registry = new CommunicationChannelRegistry();
        var channel = new StaticCommunicationChannel("fake-voice");
        registry.Register("workspace-a", channel);
        var numberStore = new DataStoreDialNumberStore(
            new Callora.Host.Backend.Application.Plugins.InMemoryPluginDataStore());
        await numberStore.AddAsync("workspace-a", "+4930111", null);
        var tracker = new DialRunTracker(new DialRunExecutor(registry), numberStore);

        var started = await tracker.StartRunAsync("workspace-a", new DialRunOptions(TimeSpan.FromSeconds(5)));
        Assert.NotNull(started);
        Assert.Equal(DialRunStatus.Running, started!.Status);

        var rejected = await tracker.StartRunAsync("workspace-a", DialRunOptions.Default);
        Assert.Null(rejected);

        var call = await WaitForPlacedCallAsync(channel, index: 0);
        call.TransitionTo(CallState.Connected);
        call.TransitionTo(CallState.Terminated);

        var completed = await tracker.WaitForCompletionAsync("workspace-a", TimeSpan.FromSeconds(15));
        Assert.NotNull(completed);
        Assert.Equal(DialRunStatus.Completed, completed!.Status);
        Assert.Equal(DialAttemptOutcome.Connected, Assert.Single(completed.Attempts).Outcome);
    }

    private static DialNumberEntry NewNumber(string number) =>
        new(Guid.NewGuid().ToString("N"), number, null, DateTimeOffset.UtcNow);

    private static async Task<StaticCall> WaitForPlacedCallAsync(StaticCommunicationChannel channel, int index)
    {
        // Großzügiges Budget: CI-Runner unter Last dürfen den Test nicht flaken lassen.
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (channel.PlacedCalls.Count > index)
                return channel.PlacedCalls[index];

            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }

        throw new TimeoutException($"Call #{index} was not placed in time.");
    }
}
