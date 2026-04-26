namespace Callora.Host.Backend.Application.Monitoring;

/// <summary>
/// Captures one violated SLO with measured and target values.
/// </summary>
/// <param name="SloKey">Stable key of the violated SLO.</param>
/// <param name="ActualValue">Measured value that breached the SLO.</param>
/// <param name="ThresholdValue">Configured SLO threshold.</param>
/// <param name="Message">Human-readable violation detail.</param>
public sealed record PluginLifecycleSloViolation(
    string SloKey,
    double ActualValue,
    double ThresholdValue,
    string Message);
