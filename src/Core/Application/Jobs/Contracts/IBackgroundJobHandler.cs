namespace Callora.Core.Application.Jobs.Contracts;

/// <summary>
/// Executes background jobs of one job type. Host services register handlers
/// in DI; plugins export them via <c>IHostPluginContext.Export</c>.
/// </summary>
/// <remarks>
/// Delivery is at-least-once: a job may run more than once (retry after a
/// failure, or crash recovery when a worker dies mid-run and the lease
/// expires). Handlers must therefore be idempotent — running the same job
/// twice must not double an external effect (send, charge, provision).
/// </remarks>
public interface IBackgroundJobHandler
{
    /// <summary>Job type this handler executes.</summary>
    string JobType { get; }

    /// <summary>
    /// Executes one job. Throwing marks the attempt as failed and triggers a
    /// retry until the attempt budget is exhausted. Must be idempotent (see
    /// the interface remarks on at-least-once delivery).
    /// </summary>
    Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default);
}
