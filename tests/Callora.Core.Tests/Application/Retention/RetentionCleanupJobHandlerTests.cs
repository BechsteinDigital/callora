using Callora.Core.Application.Jobs;
using Callora.Core.Application.Retention;
using Callora.Core.Domain.Jobs;
using Callora.Core.Tests.Support;
using Callora.Core.Application.Jobs.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Core.Tests.Application.Retention;

public sealed class RetentionCleanupJobHandlerTests
{
    [Fact]
    public async Task Execute_DeletesExpiredCompletedJobs_KeepsActiveAndYoungOnes()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var jobStore = new InMemoryBackgroundJobStore();

        var expiredSucceeded = CreateCompletedJob(nowUtc.AddDays(-30), succeeded: true);
        var expiredFailed = CreateCompletedJob(nowUtc.AddDays(-30), succeeded: false);
        var youngSucceeded = CreateCompletedJob(nowUtc.AddDays(-1), succeeded: true);
        var pending = BackgroundJob.Create("mail.send", "{}", nowUtc.AddDays(-30), 3, null, nowUtc.AddDays(-30));

        await jobStore.AddAsync(expiredSucceeded);
        await jobStore.AddAsync(expiredFailed);
        await jobStore.AddAsync(youngSucceeded);
        await jobStore.AddAsync(pending);

        var handler = new RetentionCleanupJobHandler(
            new RetentionOptions { CompletedJobRetention = TimeSpan.FromDays(14) },
            jobStore,
            new InMemoryNotificationStore(),
            NullLogger<RetentionCleanupJobHandler>.Instance);

        await handler.ExecuteAsync(CreateContext());

        var remaining = await jobStore.ListRecentAsync(10);
        Assert.Equal(2, remaining.Count);
        Assert.Contains(remaining, x => x.Id == youngSucceeded.Id);
        Assert.Contains(remaining, x => x.Id == pending.Id);
    }

    [Fact]
    public async Task Execute_DeletesExpiredNotifications()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var notificationStore = new InMemoryNotificationStore();
        await notificationStore.AddAsync(null, "Alt", "wird gelöscht", "info", nowUtc.AddDays(-120));
        await notificationStore.AddAsync(null, "Neu", "bleibt", "info", nowUtc.AddDays(-5));

        var handler = new RetentionCleanupJobHandler(
            new RetentionOptions { NotificationRetention = TimeSpan.FromDays(90) },
            new InMemoryBackgroundJobStore(),
            notificationStore,
            NullLogger<RetentionCleanupJobHandler>.Instance);

        await handler.ExecuteAsync(CreateContext());

        var remaining = await notificationStore.ListAsync(null, includeRead: true, limit: 10);
        var single = Assert.Single(remaining);
        Assert.Equal("Neu", single.Title);
    }

    private static BackgroundJob CreateCompletedJob(DateTimeOffset completedAtUtc, bool succeeded)
    {
        var job = BackgroundJob.Create(
            "webhook.deliver",
            "{\"phoneNumber\":\"+491701234567\"}",
            completedAtUtc.AddMinutes(-5),
            maxAttempts: 1,
            workspaceKey: null,
            nowUtc: completedAtUtc.AddMinutes(-5));
        job.MarkRunning(completedAtUtc.AddMinutes(-1), TimeSpan.FromMinutes(5));

        if (succeeded)
        {
            job.MarkSucceeded(completedAtUtc);
        }
        else
        {
            job.MarkFailedAttempt("boom", TimeSpan.Zero, completedAtUtc);
        }

        return job;
    }

    private static BackgroundJobExecutionContext CreateContext() => new(
        Guid.NewGuid(),
        RetentionCleanupJobHandler.JobTypeName,
        "{}",
        null,
        Attempt: 1);
}
