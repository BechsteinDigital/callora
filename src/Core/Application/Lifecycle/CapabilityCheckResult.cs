namespace Callora.Core.Application.Lifecycle;

/// <summary>
/// Outcome of one capability dependency check.
/// </summary>
public sealed record CapabilityCheckResult(
    bool IsAllowed,
    string? Message = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    /// <summary>
    /// Shared allowed instance.
    /// </summary>
    public static CapabilityCheckResult Allowed { get; } = new(true);

    /// <summary>
    /// Creates one denied result.
    /// </summary>
    public static CapabilityCheckResult Denied(string message, IReadOnlyDictionary<string, string>? metadata = null) =>
        new(false, message, metadata);
}
