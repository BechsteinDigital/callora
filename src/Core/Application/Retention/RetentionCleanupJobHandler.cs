using Callora.Core.Application.Jobs;
using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Application.Notifications;
using Callora.Core.Extensibility;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Application.Retention;

/// <summary>
/// Recurring cleanup job: deletes completed background jobs and old
/// notifications once their retention window elapsed (PLAT-240).
/// </summary>
[HostProtected]
public sealed class RetentionCleanupJobHandler(
    RetentionOptions options,
    IBackgroundJobStore jobStore,
    INotificationStore notificationStore,
    ILogger<RetentionCleanupJobHandler> logger) : IBackgroundJobHandler
{
    public const string JobTypeName = "host.retention.cleanup";

    public string JobType => JobTypeName;

    public async Task ExecuteAsync(
        BackgroundJobExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTimeOffset.UtcNow;

        var deletedJobs = await jobStore
            .DeleteCompletedBeforeAsync(nowUtc - options.CompletedJobRetention, cancellationToken)
            .ConfigureAwait(false);

        var deletedNotifications = await notificationStore
            .DeleteCreatedBeforeAsync(nowUtc - options.NotificationRetention, cancellationToken)
            .ConfigureAwait(false);

        if (deletedJobs > 0 || deletedNotifications > 0)
        {
            logger.LogInformation(
                "Retention sweep removed {DeletedJobs} completed jobs and {DeletedNotifications} notifications.",
                deletedJobs,
                deletedNotifications);
        }
    }
}
