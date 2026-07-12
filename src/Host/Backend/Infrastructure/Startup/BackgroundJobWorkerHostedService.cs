using Callora.Host.Backend.Application.Jobs;

namespace Callora.Host.Backend.Infrastructure.Startup;

/// <summary>
/// Continuously processes due background jobs, one service scope per job.
/// </summary>
public sealed class BackgroundJobWorkerHostedService(
    IServiceScopeFactory scopeFactory,
    BackgroundJobOptions options,
    ILogger<BackgroundJobWorkerHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = false;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<BackgroundJobProcessor>();
                processed = await processor.ProcessNextAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background job worker iteration failed.");
            }

            if (!processed)
            {
                try
                {
                    await Task.Delay(options.PollInterval, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
