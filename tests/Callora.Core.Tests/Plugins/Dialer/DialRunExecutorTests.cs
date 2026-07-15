using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Channels;
using Callora.Core.Tests.Support;
using Callora.Plugins.Dialer.Application.Numbers;
using Callora.Plugins.Dialer.Application.Runs;
using Xunit;

namespace Callora.Core.Tests.Plugins.Dialer;

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
    public async Task Coordinator_PersistsRunningSnapshot_EnqueuesJob_AndRejectsParallelRuns()
    {
        var dataStore = new Callora.Core.Application.Plugins.InMemoryPluginDataStore();
        var runStore = new DataStoreDialRunStore(dataStore);
        var jobQueue = new RecordingBackgroundJobQueue();
        var coordinator = new DialRunCoordinator(runStore, jobQueue);

        var started = await coordinator.StartRunAsync("workspace-a", new DialRunOptions(TimeSpan.FromSeconds(5)));

        Assert.NotNull(started);
        Assert.Equal(DialRunStatus.Running, started!.Status);
        var jobRequest = Assert.Single(jobQueue.Requests);
        Assert.Equal(DialRunJobHandler.JobTypeName, jobRequest.JobType);
        Assert.Equal("workspace-a", jobRequest.WorkspaceKey);
        Assert.Equal(1, jobRequest.MaxAttempts);

        var rejected = await coordinator.StartRunAsync("workspace-a", DialRunOptions.Default);
        Assert.Null(rejected);

        var latest = await coordinator.GetLatestRunAsync("workspace-a");
        Assert.Equal(started.RunId, latest!.RunId);
    }

    [Fact]
    public async Task JobHandler_ExecutesRun_AndPersistsCompletedSnapshot()
    {
        var registry = new CommunicationChannelRegistry();
        var channel = new StaticCommunicationChannel("fake-voice");
        registry.Register("workspace-a", channel);

        var dataStore = new Callora.Core.Application.Plugins.InMemoryPluginDataStore();
        var runStore = new DataStoreDialRunStore(dataStore);
        var numberStore = new DataStoreDialNumberStore(dataStore);
        await numberStore.AddAsync("workspace-a", "+4930111", null);

        var coordinator = new DialRunCoordinator(runStore, new RecordingBackgroundJobQueue());
        var started = await coordinator.StartRunAsync("workspace-a", new DialRunOptions(TimeSpan.FromSeconds(5)));

        var handler = new DialRunJobHandler(new DialRunExecutor(registry), numberStore, runStore);
        var handlerTask = handler.ExecuteAsync(new Callora.Core.Application.Jobs.Contracts.BackgroundJobExecutionContext(
            Guid.NewGuid(),
            DialRunJobHandler.JobTypeName,
            $$"""{"runId":"{{started!.RunId}}","workspaceKey":"workspace-a","callTimeoutSeconds":5}""",
            "workspace-a",
            Attempt: 1));

        var call = await WaitForPlacedCallAsync(channel, index: 0);
        call.TransitionTo(CallState.Connected);
        call.TransitionTo(CallState.Terminated);
        await handlerTask;

        var completed = await runStore.GetLatestAsync("workspace-a");
        Assert.NotNull(completed);
        Assert.Equal(DialRunStatus.Completed, completed!.Status);
        Assert.Equal(DialAttemptOutcome.Connected, Assert.Single(completed.Attempts).Outcome);
        Assert.Equal(started.RunId, completed.RunId);
    }

    [Fact]
    public async Task JobHandler_FailingRun_PersistsFailedSnapshot_AndRethrows()
    {
        // Kein Voice-Channel registriert: der Executor wirft.
        var registry = new CommunicationChannelRegistry();
        var dataStore = new Callora.Core.Application.Plugins.InMemoryPluginDataStore();
        var runStore = new DataStoreDialRunStore(dataStore);
        var numberStore = new DataStoreDialNumberStore(dataStore);
        await numberStore.AddAsync("workspace-a", "+4930111", null);

        var coordinator = new DialRunCoordinator(runStore, new RecordingBackgroundJobQueue());
        var started = await coordinator.StartRunAsync("workspace-a", DialRunOptions.Default);

        var handler = new DialRunJobHandler(new DialRunExecutor(registry), numberStore, runStore);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ExecuteAsync(new Callora.Core.Application.Jobs.Contracts.BackgroundJobExecutionContext(
                Guid.NewGuid(),
                DialRunJobHandler.JobTypeName,
                $$"""{"runId":"{{started!.RunId}}","workspaceKey":"workspace-a","callTimeoutSeconds":5}""",
                "workspace-a",
                Attempt: 1)));

        var failed = await runStore.GetLatestAsync("workspace-a");
        Assert.Equal(DialRunStatus.Failed, failed!.Status);
        Assert.NotNull(failed.ErrorMessage);
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
