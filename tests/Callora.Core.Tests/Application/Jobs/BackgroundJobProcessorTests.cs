using Callora.Core.Application.Jobs;
using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Domain.Jobs;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Application.Jobs;

public sealed class BackgroundJobProcessorTests
{
    [Fact]
    public async Task ProcessNext_WithoutDueJob_ReturnsFalse()
    {
        var (processor, _, _) = CreateProcessor();

        Assert.False(await processor.ProcessNextAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ProcessNext_ExecutesHandler_AndMarksSucceeded()
    {
        var handler = new RecordingBackgroundJobHandler("test.job");
        var (processor, store, _) = CreateProcessor(handler);
        var job = BackgroundJob.Create("test.job", """{"n":1}""", DateTimeOffset.UtcNow, 3, "workspace-a", DateTimeOffset.UtcNow);
        await store.AddAsync(job);

        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));

        Assert.Equal(BackgroundJobStatus.Succeeded, job.Status);
        var execution = Assert.Single(handler.Executions);
        Assert.Equal(job.Id, execution.JobId);
        Assert.Equal("""{"n":1}""", execution.PayloadJson);
        Assert.Equal("workspace-a", execution.WorkspaceKey);
        Assert.Equal(1, execution.Attempt);
    }

    [Fact]
    public async Task ProcessNext_FailedAttempt_ReschedulesUntilAttemptsExhausted()
    {
        var handler = new RecordingBackgroundJobHandler("test.job", failuresBeforeSuccess: 5);
        var (processor, store, _) = CreateProcessor(handler);
        var job = BackgroundJob.Create("test.job", "{}", DateTimeOffset.UtcNow, maxAttempts: 2, null, DateTimeOffset.UtcNow);
        await store.AddAsync(job);

        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));
        Assert.Equal(BackgroundJobStatus.Pending, job.Status);
        Assert.True(job.ScheduledAtUtc > DateTimeOffset.UtcNow);
        Assert.Equal("Simulated job failure.", job.LastError);

        // Retry sofort fällig machen und erneut verarbeiten.
        var retryJob = BackgroundJob.Create("test.job", "{}", DateTimeOffset.UtcNow, maxAttempts: 1, null, DateTimeOffset.UtcNow);
        await store.AddAsync(retryJob);
        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));
        Assert.Equal(BackgroundJobStatus.Failed, retryJob.Status);
    }

    [Fact]
    public async Task ProcessNext_WithoutHandler_FailsJobAfterAttempts()
    {
        var (processor, store, _) = CreateProcessor();
        var job = BackgroundJob.Create("unknown.job", "{}", DateTimeOffset.UtcNow, maxAttempts: 1, null, DateTimeOffset.UtcNow);
        await store.AddAsync(job);

        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));

        Assert.Equal(BackgroundJobStatus.Failed, job.Status);
        Assert.Contains("unknown.job", job.LastError);
    }

    [Fact]
    public async Task ProcessNext_ResolvesHandlersFromPluginExports()
    {
        var pluginHandler = new RecordingBackgroundJobHandler("plugin.job");
        var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IBackgroundJobHandler)] = [pluginHandler]
        });
        var store = new InMemoryBackgroundJobStore();
        var processor = new BackgroundJobProcessor(
            store,
            new BackgroundJobHandlerResolver([], catalog),
            new BackgroundJobOptions(),
            NullLogger<BackgroundJobProcessor>.Instance);
        await store.AddAsync(BackgroundJob.Create("plugin.job", "{}", DateTimeOffset.UtcNow, 1, null, DateTimeOffset.UtcNow));

        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));

        Assert.Single(pluginHandler.Executions);
    }

    private static (BackgroundJobProcessor Processor, InMemoryBackgroundJobStore Store, RecordingBackgroundJobHandler? Handler)
        CreateProcessor(RecordingBackgroundJobHandler? handler = null)
    {
        var store = new InMemoryBackgroundJobStore();
        var handlers = handler is null
            ? Array.Empty<IBackgroundJobHandler>()
            : [handler];
        var processor = new BackgroundJobProcessor(
            store,
            new BackgroundJobHandlerResolver(handlers, new StaticPluginCatalog([])),
            new BackgroundJobOptions { RetryBaseDelay = TimeSpan.FromMilliseconds(100) },
            NullLogger<BackgroundJobProcessor>.Instance);
        return (processor, store, handler);
    }
}
