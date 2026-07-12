namespace Callora.Host.PluginContracts.Application.Jobs;

/// <summary>
/// Execution context passed to one background job handler.
/// </summary>
/// <param name="JobId">Persistent job identifier.</param>
/// <param name="JobType">Handler routing key.</param>
/// <param name="PayloadJson">Raw JSON payload provided at enqueue time.</param>
/// <param name="WorkspaceKey">Optional workspace scope.</param>
/// <param name="Attempt">1-based attempt number.</param>
public sealed record BackgroundJobExecutionContext(
    Guid JobId,
    string JobType,
    string PayloadJson,
    string? WorkspaceKey,
    int Attempt);
