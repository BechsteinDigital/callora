namespace Callora.Core.Api;

/// <summary>
/// Admin login request. <see cref="WorkspaceKey"/> is optional: platform
/// operators omit it (they receive a platform-scoped session), workspace admins
/// name the workspace they want to enter (ADR-014 §3.3, §14).
/// <para>
/// <see cref="TenantKey"/> is the level between the two — the TenantAdmin of ADR-014 §18. It is used
/// only when no workspace is named: whoever names a workspace wants to work in it, and the tenant
/// level administers rather than works.
/// </para>
/// </summary>
public sealed record LoginApiRequest(
    string Login,
    string Password,
    string? WorkspaceKey = null,
    string? TenantKey = null);
