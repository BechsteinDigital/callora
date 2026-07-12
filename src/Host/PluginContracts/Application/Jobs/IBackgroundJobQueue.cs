namespace Callora.Host.PluginContracts.Application.Jobs;

/// <summary>
/// Host-provided durable job queue. Resolvable from
/// <c>IHostPluginContext.Services</c>; handlers are matched by job type.
/// </summary>
public interface IBackgroundJobQueue
{
    /// <summary>
    /// Enqueues one job and returns its id.
    /// </summary>
    Task<Guid> EnqueueAsync(BackgroundJobRequest request, CancellationToken cancellationToken = default);
}
