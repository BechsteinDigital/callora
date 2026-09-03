using Callora.Core.Extensibility;

namespace Callora.Core.Application.Security;

/// <summary>
/// The authorization outcome of a successful admin login: the session scope, an
/// optional workspace binding, the effective role, and the least-privilege
/// permission set to stamp into the token. Produced by
/// <see cref="AdminLoginResolver"/>; a <c>null</c> grant means the authenticated
/// user may not open an admin session (→ 403). Not an API contract — the wire
/// response stays <c>LoginApiResponse</c>.
/// </summary>
[CalloraInternal("Login authorization outcome — enforcement, not a plugin contract")]
public sealed record AdminLoginGrant(
    string Scope,
    string? WorkspaceKey,
    string? Role,
    IReadOnlyList<string> Permissions,
    string? TenantKey = null);
