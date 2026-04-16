namespace Callora.Host.Backend.Domain.Audit;

/// <summary>
/// Append-only audit record for plugin lifecycle and host security events.
/// </summary>
public sealed class PluginAuditLog
{
    public Guid Id { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? PluginId { get; set; }

    public bool IsSuccess { get; set; }

    public string? RequestedBy { get; set; }

    public string? Message { get; set; }

    public string? MetadataJson { get; set; }
}
