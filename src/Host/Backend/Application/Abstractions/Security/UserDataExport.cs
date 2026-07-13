namespace Callora.Host.Backend.Application.Abstractions.Security;

/// <summary>
/// Complete export of the personal data stored for one backend user (Art. 15 GDPR).
/// </summary>
public sealed record UserDataExport(
    string ExternalId,
    string? Email,
    string? DisplayName,
    DateTimeOffset CreatedAtUtc,
    string? Role,
    IReadOnlyList<UserDataExportMembership> Memberships,
    IReadOnlyList<UserDataExportAuditEntry> AuditTrail);
