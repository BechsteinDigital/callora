namespace Callora.Core.Application.Audit;

public sealed record HostAuditEntry(
    DateTimeOffset OccurredAtUtc,
    string Action,
    string? PluginId,
    bool IsSuccess,
    string? RequestedBy,
    string? Message,
    IReadOnlyDictionary<string, string>? Metadata = null);
