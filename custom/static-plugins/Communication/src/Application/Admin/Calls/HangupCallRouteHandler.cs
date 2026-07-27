using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Calls;

namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// Handles <c>POST calls/{callId}/hangup</c> — ends a live call owned by the caller's workspace.
/// Returns 204 when the hang-up was requested, 404 when no such live call is tracked.
/// </summary>
public sealed class HangupCallRouteHandler(ICallControlService callControl) : IHostAdminApiRouteHandler
{
    /// <inheritdoc />
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CallAdminScope.TryResolve(request, out var workspaceKey, out var scopeError))
        {
            return scopeError!;
        }

        if (!request.RouteValues.TryGetValue("callId", out var callId) || string.IsNullOrWhiteSpace(callId))
        {
            return new HostAdminApiResponse(400, new { error = "callId is required." });
        }

        var hungUp = await callControl.HangupAsync(workspaceKey, callId, cancellationToken).ConfigureAwait(false);
        return hungUp
            ? new HostAdminApiResponse(204)
            : new HostAdminApiResponse(404, new { error = "Call not found." });
    }
}
