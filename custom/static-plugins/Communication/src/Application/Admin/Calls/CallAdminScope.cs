using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// Reads the workspace a call-control admin request operates on. The host resolves it
/// authoritatively into <see cref="HostAdminApiRequest.WorkspaceKey"/> — the caller's bound
/// workspace when it has one, otherwise the workspace a platform operator named explicitly —
/// and has already confirmed the plugin is available there (#109). The plugin therefore
/// never reads a workspace from the query itself; that would bypass the host's gate.
/// Mirrors the SIP-account scope helper.
/// </summary>
internal static class CallAdminScope
{
    public static bool TryResolve(
        HostAdminApiRequest request,
        out string workspaceKey,
        out HostAdminApiResponse? error)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.WorkspaceKey))
        {
            workspaceKey = string.Empty;
            error = new HostAdminApiResponse(400, new { error = "A workspace is required." });
            return false;
        }

        workspaceKey = request.WorkspaceKey.Trim();
        error = null;
        return true;
    }
}
