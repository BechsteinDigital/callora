namespace Callora.Host.PluginContracts.Application.Jobs;

/// <summary>
/// Executes background jobs of one job type. Host services register handlers
/// in DI; plugins export them via <c>IHostPluginContext.Export</c>.
/// </summary>
public interface IBackgroundJobHandler
{
    /// <summary>Job type this handler executes.</summary>
    string JobType { get; }

    /// <summary>
    /// Executes one job. Throwing marks the attempt as failed and triggers a
    /// retry until the attempt budget is exhausted.
    /// </summary>
    Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default);
}
