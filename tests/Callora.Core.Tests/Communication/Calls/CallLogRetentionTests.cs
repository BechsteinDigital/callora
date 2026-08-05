using Callora.Plugin.Communication;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Domain.Calls;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Communication.Calls;

/// <summary>
/// Call history carries the remote party's number, so how long it is kept needs a bound (#117).
/// Deleting a workspace already purged its records, but an active workspace accumulated history
/// forever.
/// </summary>
public sealed class CallLogRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PurgesFinishedCallsPastTheWindow_AndKeepsRecentOnes()
    {
        var store = new RecordingCallLogStore();
        store.Added.Add(EndedCall("old", Now.AddDays(-100)));
        store.Added.Add(EndedCall("recent", Now.AddDays(-10)));

        await NewHandler(store, TimeSpan.FromDays(90)).ExecuteAsync(Context());

        Assert.Equal(["recent"], store.Added.Select(x => x.Id));
    }

    [Fact]
    public async Task NeverPurgesACallThatHasNotEnded()
    {
        // An in-progress call has no end time to measure against; deleting it would erase a live
        // conversation's history mid-call.
        var store = new RecordingCallLogStore();
        store.Added.Add(CallLog.Start(
            "live", "ws-a", "ch-1", CallDirection.Inbound, "+49301234567", "line",
            null, null, Now.AddDays(-365)));

        await NewHandler(store, TimeSpan.FromDays(1)).ExecuteAsync(Context());

        Assert.Single(store.Added);
    }

    [Fact]
    public async Task RepeatedRuns_AreIdempotent()
    {
        var store = new RecordingCallLogStore();
        store.Added.Add(EndedCall("old", Now.AddDays(-100)));
        var handler = NewHandler(store, TimeSpan.FromDays(90));

        await handler.ExecuteAsync(Context());
        await handler.ExecuteAsync(Context());

        Assert.Empty(store.Added);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-5")]
    public void UnusableConfiguration_FallsBackToTheDefault(string? configured)
    {
        // "Keep forever" has to be a deliberate choice, not the result of a typo.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CommunicationPlugin.CallLogRetentionDaysConfigKey] = configured,
            })
            .Build();

        Assert.Equal(
            CommunicationPlugin.DefaultCallLogRetention,
            CommunicationPlugin.ResolveCallLogRetention(configuration));
    }

    [Fact]
    public void ConfiguredWindow_IsHonoured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CommunicationPlugin.CallLogRetentionDaysConfigKey] = "30",
            })
            .Build();

        Assert.Equal(TimeSpan.FromDays(30), CommunicationPlugin.ResolveCallLogRetention(configuration));
    }

    private static CallLogRetentionJobHandler NewHandler(RecordingCallLogStore store, TimeSpan retention) =>
        new(store, new FakeTimeProvider(Now), retention, NullLogger<CallLogRetentionJobHandler>.Instance);

    private static Callora.Core.Application.Jobs.Contracts.BackgroundJobExecutionContext Context() =>
        new(Guid.NewGuid(), CallLogRetentionJobHandler.JobTypeName, "{}", null, Attempt: 1);

    private static CallLog EndedCall(string id, DateTimeOffset endedAt)
    {
        var log = CallLog.Start(
            id, "ws-a", "ch-1", CallDirection.Inbound, "+49301234567", "line",
            null, null, endedAt.AddMinutes(-1));
        log.End(endedAt, CallOutcome.Missed, null);
        return log;
    }
}
