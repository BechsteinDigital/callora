namespace Callora.Plugins.Voip.Application.Accounts;

/// <summary>
/// Create/update payload for one SIP account.
/// </summary>
public sealed record UpsertSipAccountRequest(
    string Username,
    string Domain,
    string DisplayName,
    string Secret,
    bool IsActive);
