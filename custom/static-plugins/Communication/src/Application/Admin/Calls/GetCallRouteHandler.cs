using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// Handles <c>GET calls/{callId}</c> — returns a live call owned by the caller's workspace, or 404 when
/// it is not tracked (already ended or never known / another workspace's call).
/// </summary>
public sealed class GetCallRouteHandler(ICallControlService callControl) : IHostAdminApiRouteHandler
{
    /// <inheritdoc />
    public ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CallAdminScope.TryResolve(request, out var workspaceKey, out var scopeError))
        {
            return ValueTask.FromResult(scopeError!);
        }

        if (!request.RouteValues.TryGetValue("callId", out var callId) || string.IsNullOrWhiteSpace(callId))
        {
            return ValueTask.FromResult(new HostAdminApiResponse(400, new { error = "callId is required." }));
        }

        var snapshot = callControl.Get(workspaceKey, callId);
        return ValueTask.FromResult(snapshot is null
            ? new HostAdminApiResponse(404, new { error = "Call not found." })
            : new HostAdminApiResponse(200, CallView.From(snapshot)));
    }
}
