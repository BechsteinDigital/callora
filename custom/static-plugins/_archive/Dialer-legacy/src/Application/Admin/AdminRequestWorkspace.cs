using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugins.Dialer.Application.Admin;

/// <summary>
/// Extracts the required workspace route value from admin API requests.
/// </summary>
internal static class AdminRequestWorkspace
{
    public static bool TryGet(
        HostAdminApiRequest request,
        out string workspaceKey,
        out HostAdminApiResponse? error)
    {
        if (request.RouteValues.TryGetValue("workspaceKey", out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            workspaceKey = value.Trim();
            error = null;
            return true;
        }

        workspaceKey = string.Empty;
        error = new HostAdminApiResponse(400, new { message = "Route value 'workspaceKey' is required." });
        return false;
    }
}
