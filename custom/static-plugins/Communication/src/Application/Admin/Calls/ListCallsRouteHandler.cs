using System.Globalization;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Calls;

namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// Handles <c>GET calls</c> — returns the caller's workspace call history, newest first. An optional
/// <c>?limit=</c> caps the page (default 50, hard-capped at 200 so a client cannot request an unbounded scan).
/// </summary>
public sealed class ListCallsRouteHandler(ICallControlService callControl) : IHostAdminApiRouteHandler
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

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

        var limit = ResolveLimit(request);
        var history = await callControl.ListRecentAsync(workspaceKey, limit, cancellationToken).ConfigureAwait(false);
        return new HostAdminApiResponse(200, history);
    }

    private static int ResolveLimit(HostAdminApiRequest request)
    {
        if (request.Query.TryGetValue("limit", out var values) &&
            values.Length > 0 &&
            int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var requested) &&
            requested > 0)
        {
            return Math.Min(requested, MaxLimit);
        }

        return DefaultLimit;
    }
}
