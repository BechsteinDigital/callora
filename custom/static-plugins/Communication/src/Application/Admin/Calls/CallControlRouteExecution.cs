using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// The shape every call-control route shares: resolve the workspace, read the call id, run the
/// operation, and map its outcome onto one status code. Accept, reject, hang-up and DTMF differ only
/// in the operation, so they differ only in what they pass here.
/// </summary>
/// <remarks>
/// The three outcomes are deliberately distinct. 404 means the workspace has no such live call. 409
/// means the call is there but the request does not apply to its current state — a client showing a
/// call list needs that difference to decide between removing a row and re-rendering it. 400 covers a
/// malformed request, which is neither.
/// </remarks>
internal static class CallControlRouteExecution
{
    public static async ValueTask<HostAdminApiResponse> RunAsync(
        HostAdminApiRequest request,
        Func<string, string, Task<bool>> operation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(operation);

        if (!CallAdminScope.TryResolve(request, out var workspaceKey, out var scopeError))
        {
            return scopeError!;
        }

        if (!request.RouteValues.TryGetValue("callId", out var callId) || string.IsNullOrWhiteSpace(callId))
        {
            return new HostAdminApiResponse(400, new { error = "callId is required." });
        }

        try
        {
            return await operation(workspaceKey, callId).ConfigureAwait(false)
                ? new HostAdminApiResponse(204)
                : new HostAdminApiResponse(404, new { error = "Call not found." });
        }
        catch (InvalidOperationException ex)
        {
            return new HostAdminApiResponse(409, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return new HostAdminApiResponse(400, new { error = ex.Message });
        }
    }
}
