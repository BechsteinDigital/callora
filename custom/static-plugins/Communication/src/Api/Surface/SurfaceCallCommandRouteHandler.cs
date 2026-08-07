using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Api.Surface;

/// <summary>
/// Handles <c>POST calls/{callId}/accept|reject|hangup</c> from a surface panel.
/// </summary>
public sealed class SurfaceCallCommandRouteHandler(ICallControlService calls, SurfaceCallCommand command)
    : IHostSurfaceApiRouteHandler
{
    /// <summary>Route value carrying the call the command applies to.</summary>
    public const string CallIdRouteKey = "callId";

    /// <inheritdoc />
    public async ValueTask<HostSurfaceApiResponse> HandleAsync(
        HostSurfaceApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SurfaceCallAccess.TryResolve(request, SurfaceCallAccess.Manage, out var workspaceKey, out var error))
        {
            return error!;
        }

        if (!request.RouteValues.TryGetValue(CallIdRouteKey, out var callId) || string.IsNullOrWhiteSpace(callId))
        {
            return new HostSurfaceApiResponse(400, new { error = "callId required" });
        }

        // A call that is not there any more is the ordinary race on a telephone line: the caller hung
        // up while somebody reached for the button.
        var done = command switch
        {
            SurfaceCallCommand.Accept => await calls.AcceptAsync(workspaceKey, callId, cancellationToken).ConfigureAwait(false),
            SurfaceCallCommand.Reject => await calls.RejectAsync(workspaceKey, callId, cancellationToken).ConfigureAwait(false),
            _ => await calls.HangupAsync(workspaceKey, callId, cancellationToken).ConfigureAwait(false),
        };

        return done
            ? new HostSurfaceApiResponse(200, new { done = true })
            : new HostSurfaceApiResponse(404, new { error = "call not found" });
    }
}
