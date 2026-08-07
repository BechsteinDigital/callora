using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Accounts;

namespace Callora.Plugin.Communication.Api.Surface;

/// <summary>
/// Handles <c>GET channels</c> from a surface — whether the phone can ring at all.
/// </summary>
/// <remarks>
/// <para>Fetched rather than pushed as context, unlike a call. A line's state is persisted and
/// authoritative: the reconciler writes every health transition onto the account. A context key
/// would be a second copy of something the database already holds, and the two would eventually
/// disagree about which one is true.</para>
/// <para>Behind the same claim as the calls. Whether the trunk is registered is not a secret worth
/// guarding on its own, but it is nothing a customer on a portal has any business seeing either.</para>
/// </remarks>
public sealed class SurfaceListChannelsRouteHandler(ISipAccountStore accounts) : IHostSurfaceApiRouteHandler
{
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

        var lines = await accounts.ListAsync(workspaceKey, cancellationToken).ConfigureAwait(false);

        SurfaceChannelView[] view =
        [
            .. lines.Select(line => new SurfaceChannelView(
                line.Id,
                line.DisplayName,
                line.Status.ToString(),
                line.LastStatusChangeAt,
                line.LastRegisteredAt,
                line.LastError))
        ];

        return new HostSurfaceApiResponse(200, view);
    }
}
