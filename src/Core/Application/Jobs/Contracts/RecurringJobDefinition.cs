namespace Callora.Core.Application.Jobs.Contracts;

/// <summary>
/// One fixed-interval recurring job. The scheduler enqueues a normal
/// background job whenever the interval elapsed and no job of the same
/// type is still pending or running.
/// </summary>
/// <param name="JobType">Job type key the handler is keyed on.</param>
/// <param name="PayloadJson">JSON payload passed to each enqueued job.</param>
/// <param name="Interval">Fixed interval between enqueues.</param>
/// <param name="MaxAttempts">Maximum attempts per enqueued job.</param>
/// <param name="WorkspaceKey">Optional workspace the job runs for; null for host-wide.</param>
public sealed record RecurringJobDefinition(
    string JobType,
    string PayloadJson,
    TimeSpan Interval,
    int MaxAttempts = 1,
    string? WorkspaceKey = null);
