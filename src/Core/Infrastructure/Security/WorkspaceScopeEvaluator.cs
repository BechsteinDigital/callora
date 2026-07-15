using System.Security.Claims;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Decides whether a principal may act on a workspace. Fail-closed: platform
/// access requires an explicit positive signal (super-admin role or a
/// <see cref="BackendClaimTypes.CalloraScope"/> claim of "platform"); a
/// principal carrying neither that nor a workspace binding is rejected.
/// </summary>
public static class WorkspaceScopeEvaluator
{
    /// <summary>
    /// True for platform operators: super admins and sessions stamped with the
    /// platform scope at issuance. Required for global- and tenant-scoped
    /// mutations. A missing scope claim never grants operator access.
    /// </summary>
    public static bool IsOperator(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.IsInRole(BackendRoles.SuperAdmin) ||
               user.HasClaim(BackendClaimTypes.CalloraScope, BackendAuthScopes.Platform);
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
