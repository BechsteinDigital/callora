namespace Callora.Plugin.Communication.Application.Admin;

public sealed record SipAccountApiModel(
    string SipAccountId,
    string Username,
    string Domain,
    string DisplayName,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
