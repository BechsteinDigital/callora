using Callora.Core.Application.Jobs;
using Callora.Core.Tests.Support;
using Callora.Core.Application.Jobs.Contracts;
using Xunit;

namespace Callora.Core.Tests.Application.Jobs;

public sealed class RecurringJobEnqueuerTests
{
    [Fact]
    public async Task FirstEvaluation_DoesNotEnqueue_SecondAfterIntervalDoes()
    {
        var provider = new StaticRecurringJobProvider(
            new RecurringJobDefinition("recurring.job", "{}", TimeSpan.FromMinutes(5)));
        var enqueuer = new RecurringJobEnqueuer([provider], new StaticPluginCatalog([]));
        var store = new InMemoryBackgroundJobStore();
        var start = DateTimeOffset.UtcNow;

        await enqueuer.EnqueueDueJobsAsync(store, start, CancellationToken.None);
        Assert.Empty(await store.ListRecentAsync(10));

        await enqueuer.EnqueueDueJobsAsync(store, start + TimeSpan.FromMinutes(6), CancellationToken.None);
        Assert.Single(await store.ListRecentAsync(10));
    }

    [Fact]
    public async Task DueJob_IsSkipped_WhileSameTypeIsStillActive()
    {
        var provider = new StaticRecurringJobProvider(
            new RecurringJobDefinition("recurring.job", "{}", TimeSpan.FromMinutes(5)));
        var enqueuer = new RecurringJobEnqueuer([provider], new StaticPluginCatalog([]));
        var store = new InMemoryBackgroundJobStore();
        var start = DateTimeOffset.UtcNow;

        await enqueuer.EnqueueDueJobsAsync(store, start, CancellationToken.None);
        await enqueuer.EnqueueDueJobsAsync(store, start + TimeSpan.FromMinutes(6), CancellationToken.None);
        Assert.Single(await store.ListRecentAsync(10));

        // Der erste Job ist weiterhin Pending → nächster Zyklus legt keinen zweiten an.
        await enqueuer.EnqueueDueJobsAsync(store, start + TimeSpan.FromMinutes(12), CancellationToken.None);
        Assert.Single(await store.ListRecentAsync(10));
    }
}
