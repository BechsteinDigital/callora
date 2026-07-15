namespace Callora.Core.Application.Lifecycle;

/// <summary>
/// Outcome of one runtime extension registration sync or validation.
/// </summary>
public sealed record ExtensionSyncResult(
    bool IsSuccess,
    string? Message = null,
    string? ErrorCode = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    /// <summary>
    /// Shared success instance.
    /// </summary>
    public static ExtensionSyncResult Success { get; } = new(true);
}
