namespace Callora.Host.Backend.Application.Monitoring;

/// <summary>
/// Represents one aggregated plugin lifecycle measurement window.
/// </summary>
/// <param name="TotalOperationCount">Total lifecycle operations in the window.</param>
/// <param name="FailedOperationCount">Failed lifecycle operations in the window.</param>
/// <param name="ActivationP95DurationMs">p95 activation duration in milliseconds.</param>
public sealed record PluginLifecycleSloSnapshot(
    int TotalOperationCount,
    int FailedOperationCount,
    double ActivationP95DurationMs);
