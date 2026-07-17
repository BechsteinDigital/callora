namespace Callora.Core.Api;

/// <summary>
/// Admin login request. <see cref="WorkspaceKey"/> is optional: platform
/// operators omit it (they receive a platform-scoped session), workspace admins
/// name the workspace they want to enter (ADR-014 §3.3, §14).
/// </summary>
public sealed record LoginApiRequest(
    string Login,
    string Password,
    string? WorkspaceKey = null);
