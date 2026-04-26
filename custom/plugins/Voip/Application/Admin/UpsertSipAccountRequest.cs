namespace Callora.Plugins.Voip.Application.Admin;

public sealed record UpsertSipAccountRequest(
    string Username,
    string Domain,
    string DisplayName,
    string Secret,
    bool IsActive);
