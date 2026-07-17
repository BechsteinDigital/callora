namespace Callora.Core.Application.Monitoring;

/// <summary>
/// Evaluates plugin lifecycle SLO compliance against configured targets.
/// </summary>
public sealed class PluginLifecycleSloEvaluator
{
    /// <summary>
    /// Evaluates one aggregated lifecycle metrics snapshot.
    /// </summary>
    /// <param name="snapshot">Aggregated measurement window.</param>
    /// <returns>Evaluation result with zero or more SLO violations.</returns>
    public PluginLifecycleSloEvaluationResult Evaluate(PluginLifecycleSloSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.TotalOperationCount <= 0)
        {
            return new PluginLifecycleSloEvaluationResult(0, 0d, 1d, []);
        }

        var failures = Math.Clamp(snapshot.FailedOperationCount, 0, snapshot.TotalOperationCount);
        var errorRate = failures / (double)snapshot.TotalOperationCount;
        var successRate = 1d - errorRate;

        if (snapshot.TotalOperationCount < PluginLifecycleSloTargets.MinimumSampleSize)
        {
            return new PluginLifecycleSloEvaluationResult(
                snapshot.TotalOperationCount,
                errorRate,
                successRate,
                []);
        }

        var violations = new List<PluginLifecycleSloViolation>(capacity: 3);

        if (snapshot.ActivationP95DurationMs > PluginLifecycleSloTargets.ActivationLatencyP95Ms)
        {
            violations.Add(new PluginLifecycleSloViolation(
                "activation_latency_p95_ms",
                snapshot.ActivationP95DurationMs,
                PluginLifecycleSloTargets.ActivationLatencyP95Ms,
                $"Activation p95 latency is {snapshot.ActivationP95DurationMs:F2}ms and exceeds {PluginLifecycleSloTargets.ActivationLatencyP95Ms:F2}ms."));
        }

        if (errorRate > PluginLifecycleSloTargets.ErrorRate)
        {
            violations.Add(new PluginLifecycleSloViolation(
                "lifecycle_error_rate",
                errorRate,
                PluginLifecycleSloTargets.ErrorRate,
                $"Lifecycle error rate is {errorRate:P2} and exceeds {PluginLifecycleSloTargets.ErrorRate:P2}."));
        }

        if (successRate < PluginLifecycleSloTargets.StabilitySuccessRate)
        {
            violations.Add(new PluginLifecycleSloViolation(
                "lifecycle_stability_success_rate",
                successRate,
                PluginLifecycleSloTargets.StabilitySuccessRate,
                $"Lifecycle stability success rate is {successRate:P2} and is below {PluginLifecycleSloTargets.StabilitySuccessRate:P2}."));
        }

        return new PluginLifecycleSloEvaluationResult(
            snapshot.TotalOperationCount,
            errorRate,
            successRate,
            violations);
    }
}
