using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// Handles <c>GET calls/active</c> — every call the workspace has in flight right now.
/// </summary>
/// <remarks>
/// History (<c>GET calls</c>) answers what happened; this answers what is happening. A dialer needs
/// both, and a client that reconnects to the event stream needs this one first — otherwise it would
/// only learn about calls that change after it connected.
/// </remarks>
public sealed class ListActiveCallsRouteHandler(ICallControlService callControl) : IHostAdminApiRouteHandler
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

        var calls = callControl.ListActive(workspaceKey).Select(CallView.From).ToArray();
        return ValueTask.FromResult(new HostAdminApiResponse(200, calls));
    }
}
