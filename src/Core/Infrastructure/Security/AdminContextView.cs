using System.Security.Claims;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// The effective administration context derived from the authenticated principal:
/// identity, effective roles and permissions, the session scope and workspace
/// binding, and whether the caller is a platform operator. The admin shell reads
/// this to drive navigation and visibility; server-side authorization stays
/// authoritative (UI hiding is not a security boundary, ADR-014 §3.4).
/// </summary>
public sealed record AdminContextView(
    string UserId,
    string? DisplayName,
    string? Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    string? Scope,
    string? WorkspaceKey,
    bool IsOperator)
{
    /// <summary>
    /// Builds the context from the principal's claims, or <c>null</c> when the
    /// principal carries no subject (unauthenticated).
    /// </summary>
    public static AdminContextView? FromPrincipal(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return new AdminContextView(
            UserId: userId,
            DisplayName: user.FindFirst(ClaimTypes.Name)?.Value,
            Email: user.FindFirst(ClaimTypes.Email)?.Value,
            Roles: user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray(),
            Permissions: user.FindAll(BackendClaimTypes.Permission).Select(claim => claim.Value).ToArray(),
            Scope: user.FindFirst(BackendClaimTypes.CalloraScope)?.Value,
            WorkspaceKey: user.FindFirst(BackendClaimTypes.WorkspaceKey)?.Value,
            IsOperator: WorkspaceScopeEvaluator.IsOperator(user));
    }
}
