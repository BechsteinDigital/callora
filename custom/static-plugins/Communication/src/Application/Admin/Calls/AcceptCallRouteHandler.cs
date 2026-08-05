using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// Handles <c>POST calls/{callId}/accept</c> — answers a ringing inbound call owned by the caller's
/// workspace. Returns 204 once the answer was requested, 404 when no such live call is tracked and
/// 409 when the call exists but is not a ringing inbound one.
/// </summary>
public sealed class AcceptCallRouteHandler(ICallControlService callControl) : IHostAdminApiRouteHandler
{
    /// <inheritdoc />
    public ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default) =>
        CallControlRouteExecution.RunAsync(
            request,
            (workspaceKey, callId) => callControl.AcceptAsync(workspaceKey, callId, cancellationToken));
}
