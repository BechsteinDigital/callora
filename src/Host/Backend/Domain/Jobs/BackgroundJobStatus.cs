namespace Callora.Host.Backend.Domain.Jobs;

/// <summary>
/// Lifecycle status of one background job.
/// </summary>
public enum BackgroundJobStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
}
