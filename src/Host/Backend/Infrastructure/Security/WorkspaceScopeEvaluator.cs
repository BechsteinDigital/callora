using System.Security.Claims;

namespace Callora.Host.Backend.Infrastructure.Security;

/// <summary>
/// Decides whether a principal may act on a workspace. Workspace logins carry
/// a <see cref="BackendClaimTypes.WorkspaceKey"/> claim and are locked to that
/// workspace; operator logins without the claim keep platform-wide access.
/// </summary>
public static class WorkspaceScopeEvaluator
{
    public static bool HasWorkspaceAccess(ClaimsPrincipal user, string? requestedWorkspaceKey)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.IsInRole(BackendRoles.Admin))
        {
            return true;
        }

        var boundWorkspaceKey = user.FindFirst(BackendClaimTypes.WorkspaceKey)?.Value;
        if (string.IsNullOrWhiteSpace(boundWorkspaceKey))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(requestedWorkspaceKey) &&
               string.Equals(
                   boundWorkspaceKey.Trim(),
                   requestedWorkspaceKey.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }
}
