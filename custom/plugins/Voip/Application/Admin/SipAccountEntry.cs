namespace Callora.Plugins.Voip.Application.Admin;

public sealed record SipAccountEntry(
    string SipAccountId,
    string Username,
    string Domain,
    string DisplayName,
    string Secret,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
