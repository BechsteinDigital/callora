using Callora.Core.Application.Jobs;
using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Jobs;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Application.Jobs;

/// <summary>
/// A revoked entitlement darkens a plugin's HTTP routes (REV2 §13). Its queued work
/// used to keep running regardless — webhooks delivered, mail sent, data synced for a
/// plugin the workspace no longer holds. These tests pin the other half of the gate.
/// </summary>
public sealed class NoJobOfAnUnavailablePluginRunsTests
{
    [Fact]
    public async Task An_unavailable_plugins_job_is_parked_rather_than_executed()
    {
        var handler = new RecordingBackgroundJobHandler("plugin.job");
        var (processor, store) = CreateProcessor(handler, unavailable: "billed-plugin");
        var job = BackgroundJob.Create("plugin.job", "{}", DateTimeOffset.UtcNow, 3, "workspace-a", DateTimeOffset.UtcNow);
        await store.AddAsync(job);

        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));

        Assert.Empty(handler.Executions);
        Assert.Equal(BackgroundJobStatus.Pending, job.Status);
    }

    [Fact]
    public async Task Parking_does_not_spend_a_retry_attempt()
    {
        // The reason parking is not "fail the attempt": a billing outage would otherwise
        // burn the retry budget and lose the work permanently. The desired state must
        // survive the lapse, exactly as the entitlement applier preserves activation.
        var handler = new RecordingBackgroundJobHandler("plugin.job");
        var (processor, store) = CreateProcessor(handler, unavailable: "billed-plugin");
        var job = BackgroundJob.Create("plugin.job", "{}", DateTimeOffset.UtcNow, maxAttempts: 1, "workspace-a", DateTimeOffset.UtcNow);
        await store.AddAsync(job);

        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));

        Assert.Equal(0, job.AttemptCount);
        Assert.NotEqual(BackgroundJobStatus.Failed, job.Status);
    }

    [Fact]
    public async Task A_restored_entitlement_lets_the_parked_job_run()
    {
        var handler = new RecordingBackgroundJobHandler("plugin.job");
        var availability = new MutablePluginAvailabilityEvaluator("billed-plugin");
        var store = new InMemoryBackgroundJobStore();
        var processor = CreateProcessor(store, handler, availability);
        var job = BackgroundJob.Create("plugin.job", "{}", DateTimeOffset.UtcNow, 3, "workspace-a", DateTimeOffset.UtcNow);
        await store.AddAsync(job);

        await processor.ProcessNextAsync(CancellationToken.None);
        Assert.Empty(handler.Executions);

        availability.RestoreAll();
        await processor.ProcessNextAsync(CancellationToken.None);

        Assert.Single(handler.Executions);
        Assert.Equal(BackgroundJobStatus.Succeeded, job.Status);
    }

    [Fact]
    public async Task An_available_plugins_job_runs_unchanged()
    {
        var handler = new RecordingBackgroundJobHandler("plugin.job");
        var (processor, store) = CreateProcessor(handler, unavailable: "some-other-plugin");
        var job = BackgroundJob.Create("plugin.job", "{}", DateTimeOffset.UtcNow, 3, "workspace-a", DateTimeOffset.UtcNow);
        await store.AddAsync(job);

        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));

        Assert.Single(handler.Executions);
        Assert.Equal(BackgroundJobStatus.Succeeded, job.Status);
    }

    [Fact]
    public async Task A_host_owned_job_is_never_gated()
    {
        // The gate keys on the export's owning plugin. A handler the host itself
        // registers has no owner, so no entitlement can lapse for it.
        var handler = new RecordingBackgroundJobHandler("host.job");
        var store = new InMemoryBackgroundJobStore();
        var processor = new BackgroundJobProcessor(
            store,
            new BackgroundJobHandlerResolver([handler], new StaticPluginCatalog([])),
            new BackgroundJobOptions { RetryBaseDelay = TimeSpan.FromMilliseconds(100) },
            NullLogger<BackgroundJobProcessor>.Instance,
            availability: new StaticPluginAvailabilityEvaluator("billed-plugin"));
        await store.AddAsync(BackgroundJob.Create("host.job", "{}", DateTimeOffset.UtcNow, 3, "workspace-a", DateTimeOffset.UtcNow));

        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));

        Assert.Single(handler.Executions);
    }

    [Fact]
    public async Task A_platform_wide_job_is_judged_on_the_platform_verdict()
    {
        // Was ungated until the platform verdict existed. A job carrying no workspace key
        // asks "may this plugin work on this host at all", which is answerable — installed,
        // healthy, entitled on the default tenant, within budget.
        var handler = new RecordingBackgroundJobHandler("plugin.job");
        var (processor, store) = CreateProcessor(handler, unavailable: "billed-plugin");
        var job = BackgroundJob.Create("plugin.job", "{}", DateTimeOffset.UtcNow, 3, workspaceKey: null, DateTimeOffset.UtcNow);
        await store.AddAsync(job);

        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));

        Assert.Empty(handler.Executions);
        Assert.Equal(BackgroundJobStatus.Pending, job.Status);
        Assert.Equal(0, job.AttemptCount);
    }

    [Fact]
    public async Task A_platform_wide_job_of_an_available_plugin_runs()
    {
        var handler = new RecordingBackgroundJobHandler("plugin.job");
        var (processor, store) = CreateProcessor(handler, unavailable: "some-other-plugin");
        await store.AddAsync(BackgroundJob.Create("plugin.job", "{}", DateTimeOffset.UtcNow, 3, workspaceKey: null, DateTimeOffset.UtcNow));

        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));

        Assert.Single(handler.Executions);
    }

    private static (BackgroundJobProcessor Processor, InMemoryBackgroundJobStore Store) CreateProcessor(
        RecordingBackgroundJobHandler handler,
        string unavailable)
    {
        var store = new InMemoryBackgroundJobStore();
        return (CreateProcessor(store, handler, new StaticPluginAvailabilityEvaluator(unavailable)), store);
    }

    private static BackgroundJobProcessor CreateProcessor(
        InMemoryBackgroundJobStore store,
        RecordingBackgroundJobHandler handler,
        IPluginAvailabilityEvaluator availability)
    {
        var catalog = new StaticPluginCatalog(
            new Dictionary<Type, IReadOnlyList<object>> { [typeof(IBackgroundJobHandler)] = [handler] },
            pluginId: "billed-plugin");
        return new BackgroundJobProcessor(
            store,
            new BackgroundJobHandlerResolver([], catalog),
            new BackgroundJobOptions
            {
                RetryBaseDelay = TimeSpan.FromMilliseconds(100),
                // Zero, so the restoration test finds the parked job due again on the very
                // next poll instead of sleeping through a real wait.
                UnavailableRetryDelay = TimeSpan.Zero
            },
            NullLogger<BackgroundJobProcessor>.Instance,
            availability: availability);
    }
}
