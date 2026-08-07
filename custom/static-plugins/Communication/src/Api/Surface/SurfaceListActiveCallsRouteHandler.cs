using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Api.Surface;

/// <summary>
/// Handles <c>GET calls/active</c> from a surface — what is going on right now.
/// </summary>
/// <remarks>
/// The context keys report changes; this is the starting point. Somebody who reloads the page mid
/// conversation would otherwise face an empty panel until the next transition, which on a quiet call
/// is when it ends.
/// </remarks>
public sealed class SurfaceListActiveCallsRouteHandler(ICallControlService calls) : IHostSurfaceApiRouteHandler
{
    /// <inheritdoc />
    public ValueTask<HostSurfaceApiResponse> HandleAsync(
        HostSurfaceApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SurfaceCallAccess.TryResolve(request, SurfaceCallAccess.Read, out var workspaceKey, out var error))
        {
            return ValueTask.FromResult(error!);
        }

        return ValueTask.FromResult(new HostSurfaceApiResponse(200, calls.ListActive(workspaceKey)));
    }
}
