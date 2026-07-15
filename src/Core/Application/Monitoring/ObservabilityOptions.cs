namespace Callora.Core.Application.Monitoring;

/// <summary>
/// Options for telemetry export and service identity.
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>Logical service name reported in telemetry resources.</summary>
    public string ServiceName { get; set; } = "callora-host";

    /// <summary>
    /// OTLP collector endpoint (gRPC), for example "http://localhost:4317".
    /// Export is disabled when empty.
    /// </summary>
    public string? OtlpEndpoint { get; set; }
}
