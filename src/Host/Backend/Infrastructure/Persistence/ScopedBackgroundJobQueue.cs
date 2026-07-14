using Callora.Host.Backend.Application.Jobs;
using Callora.Host.Backend.Domain.Jobs;
using Callora.Host.PluginContracts.Application.Jobs;

namespace Callora.Host.Backend.Infrastructure.Persistence;

/// <summary>
/// Singleton queue facade over the scoped job store. Creates one service
/// scope per enqueue so plugins can resolve <see cref="IBackgroundJobQueue"/>
/// from the root provider.
/// </summary>
public sealed class ScopedBackgroundJobQueue(IServiceScopeFactory scopeFactory) : IBackgroundJobQueue
{
    public async Task<Guid> EnqueueAsync(BackgroundJobRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nowUtc = DateTimeOffset.UtcNow;
        var job = BackgroundJob.Create(
            request.JobType,
            request.PayloadJson,
            scheduledAtUtc: request.RunAtUtc ?? nowUtc,
            maxAttempts: request.MaxAttempts,
            workspaceKey: request.WorkspaceKey,
            nowUtc: nowUtc);

        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBackgroundJobStore>();
        await store.AddAsync(job, cancellationToken).ConfigureAwait(false);
        return job.Id;
    }
}
