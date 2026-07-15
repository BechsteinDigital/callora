using Callora.Core.Application.Monitoring;

namespace Callora.Core.Tests.Application.Monitoring;

public sealed class PluginLifecycleSloEvaluatorTests
{
    [Fact]
    public void Evaluate_WhenThresholdsAreViolated_ReturnsViolations()
    {
        var evaluator = new PluginLifecycleSloEvaluator();
        var snapshot = new PluginLifecycleSloSnapshot(
            TotalOperationCount: 500,
            FailedOperationCount: 20,
            ActivationP95DurationMs: 900d);

        var result = evaluator.Evaluate(snapshot);

        Assert.False(result.IsCompliant);
        Assert.False(result.IsSampleSizeInsufficient);
        Assert.Contains(result.Violations, x => x.SloKey == "activation_latency_p95_ms");
        Assert.Contains(result.Violations, x => x.SloKey == "lifecycle_error_rate");
        Assert.Contains(result.Violations, x => x.SloKey == "lifecycle_stability_success_rate");
    }

    [Fact]
    public void Evaluate_WhenSampleSizeIsTooSmall_SkipsViolationEvaluation()
    {
        var evaluator = new PluginLifecycleSloEvaluator();
        var snapshot = new PluginLifecycleSloSnapshot(
            TotalOperationCount: 50,
            FailedOperationCount: 10,
            ActivationP95DurationMs: 2000d);

        var result = evaluator.Evaluate(snapshot);

        Assert.True(result.IsSampleSizeInsufficient);
        Assert.True(result.IsCompliant);
        Assert.Empty(result.Violations);
    }
}
