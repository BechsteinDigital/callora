namespace Callora.Host.Backend.Application.Security;

/// <summary>One audit action attributed to the user in a data export.</summary>
public sealed record UserDataExportAuditEntry(
    DateTimeOffset OccurredAtUtc,
    string Action,
    string? PluginId,
    bool IsSuccess);
