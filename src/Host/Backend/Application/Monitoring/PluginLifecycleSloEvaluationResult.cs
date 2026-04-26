namespace Callora.Host.Backend.Application.Monitoring;

/// <summary>
/// Result of one plugin lifecycle SLO evaluation.
/// </summary>
/// <param name="SampleSize">Evaluated number of lifecycle operations.</param>
/// <param name="ErrorRate">Measured error rate for the sample.</param>
/// <param name="StabilitySuccessRate">Measured success rate for the sample.</param>
/// <param name="Violations">Detected SLO violations.</param>
public sealed record PluginLifecycleSloEvaluationResult(
    int SampleSize,
    double ErrorRate,
    double StabilitySuccessRate,
    IReadOnlyList<PluginLifecycleSloViolation> Violations)
{
    /// <summary>
    /// Indicates whether all SLO targets are satisfied.
    /// </summary>
    public bool IsCompliant => Violations.Count == 0;

    /// <summary>
    /// Indicates whether SLO evaluation was skipped due to insufficient sample size.
    /// </summary>
    public bool IsSampleSizeInsufficient => SampleSize < PluginLifecycleSloTargets.MinimumSampleSize;
}
