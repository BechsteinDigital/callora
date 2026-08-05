using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// Handles <c>POST calls/{callId}/reject</c> — turns a ringing inbound call away so the caller hears
/// a decision instead of ringing out. Same status mapping as
/// <see cref="AcceptCallRouteHandler"/>.
/// </summary>
public sealed class RejectCallRouteHandler(ICallControlService callControl) : IHostAdminApiRouteHandler
{
    /// <inheritdoc />
    public ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default) =>
        CallControlRouteExecution.RunAsync(
            request,
            (workspaceKey, callId) => callControl.RejectAsync(workspaceKey, callId, cancellationToken));
}
