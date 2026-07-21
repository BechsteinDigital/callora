namespace Callora.Plugin.Communication.Application.Accounts;

/// <summary>
/// One configured SIP account within a workspace.
/// </summary>
public sealed record SipAccountEntry(
    string SipAccountId,
    string Username,
    string Domain,
    string DisplayName,
    string Secret,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
