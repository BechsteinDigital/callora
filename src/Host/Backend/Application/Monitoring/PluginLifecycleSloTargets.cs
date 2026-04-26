namespace Callora.Host.Backend.Application.Monitoring;

/// <summary>
/// Defines SLO thresholds for plugin lifecycle reliability monitoring.
/// </summary>
public static class PluginLifecycleSloTargets
{
    /// <summary>
    /// Rolling window size used for SLO evaluation and alerting.
    /// </summary>
    public const int EvaluationWindowMinutes = 15;

    /// <summary>
    /// Minimum number of lifecycle operations required before evaluating SLOs.
    /// </summary>
    public const int MinimumSampleSize = 200;

    /// <summary>
    /// Upper bound for p95 activation duration in milliseconds.
    /// </summary>
    public const double ActivationLatencyP95Ms = 750d;

    /// <summary>
    /// Upper bound for lifecycle operation error rate in the evaluation window.
    /// </summary>
    public const double ErrorRate = 0.02d;

    /// <summary>
    /// Lower bound for lifecycle operation success rate in the evaluation window.
    /// </summary>
    public const double StabilitySuccessRate = 0.995d;
}
