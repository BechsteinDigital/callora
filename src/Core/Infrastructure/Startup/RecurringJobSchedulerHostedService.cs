using Callora.Core.Application.Jobs;

namespace Callora.Core.Infrastructure.Startup;

/// <summary>
/// Periodically evaluates recurring job definitions and enqueues due jobs.
/// </summary>
public sealed class RecurringJobSchedulerHostedService(
    IServiceScopeFactory scopeFactory,
    RecurringJobEnqueuer enqueuer,
    BackgroundJobOptions options,
    ILogger<RecurringJobSchedulerHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var jobStore = scope.ServiceProvider.GetRequiredService<IBackgroundJobStore>();
                await enqueuer.EnqueueDueJobsAsync(jobStore, DateTimeOffset.UtcNow, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Recurring job scheduler iteration failed.");
            }

            try
            {
                await Task.Delay(options.SchedulerInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
