using System.Globalization;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Api.Surface;

/// <summary>
/// Handles <c>GET calls</c> from a surface — the workspace's recent calls, newest first.
/// </summary>
/// <remarks>
/// Capped like the operator route, and for the same reason: a panel asking for everything would scan
/// a table that grows with every conversation the business has.
/// </remarks>
public sealed class SurfaceListCallsRouteHandler(ICallHistory history) : IHostSurfaceApiRouteHandler
{
    private const int DefaultLimit = 25;
    private const int MaxLimit = 100;

    /// <inheritdoc />
    public async ValueTask<HostSurfaceApiResponse> HandleAsync(
        HostSurfaceApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SurfaceCallAccess.TryResolve(request, SurfaceCallAccess.Read, out var workspaceKey, out var error))
        {
            return error!;
        }

        var recent = await history
            .ListRecentAsync(workspaceKey, ResolveLimit(request), cancellationToken)
            .ConfigureAwait(false);

        return new HostSurfaceApiResponse(200, recent);
    }

    private static int ResolveLimit(HostSurfaceApiRequest request) =>
        request.Query.TryGetValue("limit", out var values) &&
        values.Length > 0 &&
        int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var requested) &&
        requested > 0
            ? Math.Min(requested, MaxLimit)
            : DefaultLimit;
}
