using Callora.Core.Extensibility;
using System.Security.Claims;

namespace Callora.Core.Application.Security;

/// <summary>
/// Decides whether a principal may act on a workspace. Fail-closed: platform
/// access requires an explicit positive signal (super-admin role or a
/// <see cref="BackendClaimTypes.CalloraScope"/> claim of "platform"); a
/// principal carrying neither that nor a workspace binding is rejected.
/// </summary>
[CalloraInternal("Workspace-scope enforcement — not a plugin contract (REV2 §7.2)")]
public static class WorkspaceScopeEvaluator
{
    /// <summary>
    /// True for platform operators: super admins and sessions stamped with the
    /// platform scope at issuance. Required for global- and tenant-scoped
    /// mutations. A missing scope claim never grants operator access.
    /// <para>
    /// This governs <em>spatial reach</em> (may the principal act across
    /// workspaces), not <em>authority</em> (what it may do). Operator status
    /// does not bypass permission checks: <c>RequirePermission</c> is bypassed
    /// only by the super-admin role — every other operator still needs the
    /// concrete permission, projected from its RBAC role at request time.
    /// </para>
    /// </summary>
    public static bool IsOperator(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.IsInRole(BackendRoles.SuperAdmin) ||
               user.HasClaim(BackendClaimTypes.CalloraScope, BackendAuthScopes.Platform);
    }

    /// <summary>
    /// True when the principal may act on the named tenant: an operator, or a tenant session bound
    /// to exactly that tenant. Pure and synchronous — the tenant key travels in the claim, so no
    /// lookup is needed, unlike <see cref="WorkspaceReach"/>.
    /// </summary>
    public static bool HasTenantAccess(ClaimsPrincipal user, string? requestedTenantKey)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (IsOperator(user))
        {
            return true;
        }

        if (!user.HasClaim(BackendClaimTypes.CalloraScope, BackendAuthScopes.Tenant))
        {
            return false;
        }

        var boundTenantKey = user.FindFirst(BackendClaimTypes.TenantKey)?.Value;
        if (string.IsNullOrWhiteSpace(boundTenantKey))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(requestedTenantKey) &&
               string.Equals(
                   boundTenantKey.Trim(),
                   requestedTenantKey.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasWorkspaceAccess(ClaimsPrincipal user, string? requestedWorkspaceKey)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (IsOperator(user))
        {
            return true;
        }

        var boundWorkspaceKey = user.FindFirst(BackendClaimTypes.WorkspaceKey)?.Value;
        if (string.IsNullOrWhiteSpace(boundWorkspaceKey))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(requestedWorkspaceKey) &&
               string.Equals(
                   boundWorkspaceKey.Trim(),
                   requestedWorkspaceKey.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }
}
