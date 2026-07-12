namespace Callora.Host.PluginContracts.Application.Jobs;

/// <summary>
/// One fixed-interval recurring job. The scheduler enqueues a normal
/// background job whenever the interval elapsed and no job of the same
/// type is still pending or running.
/// </summary>
public sealed record RecurringJobDefinition(
    string JobType,
    string PayloadJson,
    TimeSpan Interval,
    int MaxAttempts = 1,
    string? WorkspaceKey = null);
