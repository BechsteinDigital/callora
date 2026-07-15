using System.Security.Claims;

namespace Callora.Workspace.Api;

internal static class WorkspaceClaims
{
    public static string? ResolveWorkspaceKey(ClaimsPrincipal user)
    {
        return user.FindFirst("workspace_key")?.Value;
    }
}
