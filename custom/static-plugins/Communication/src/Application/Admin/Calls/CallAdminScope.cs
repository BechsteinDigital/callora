using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// Resolves the workspace a call-control admin request operates on. The caller's token-bound workspace
/// (<see cref="HostAdminApiRequest.WorkspaceKey"/>, set authoritatively by the host) always wins, so a
/// workspace-scoped operator can never reach another workspace. A platform operator (no bound
/// workspace) must name the target explicitly via <c>?workspaceKey=</c>; absent that the request is
/// rejected rather than defaulting to something dangerous. Mirrors the SIP-account scope helper.
/// </summary>
internal static class CallAdminScope
{
    public static bool TryResolve(
        HostAdminApiRequest request,
        out string workspaceKey,
        out HostAdminApiResponse? error)
    {
        var resolved = request.WorkspaceKey;
        if (string.IsNullOrWhiteSpace(resolved) &&
            request.Query.TryGetValue("workspaceKey", out var values) &&
            values.Length > 0)
        {
            resolved = values[0];
        }

        if (string.IsNullOrWhiteSpace(resolved))
        {
            workspaceKey = string.Empty;
            error = new HostAdminApiResponse(400, new { error = "A workspace is required." });
            return false;
        }

        workspaceKey = resolved.Trim();
        error = null;
        return true;
    }
}
