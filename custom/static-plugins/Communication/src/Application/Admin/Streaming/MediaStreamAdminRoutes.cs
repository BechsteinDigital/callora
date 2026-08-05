using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Streaming;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Application.Admin.Streaming;

/// <summary>
/// Builds the media-ticket Admin-API route. Registered next to the call routes because minting is a
/// call-control operation: it hands out live access to a conversation, so it carries
/// <see cref="CommunicationPermissionKeys.CallsManage"/> rather than a read permission.
/// </summary>
public static class MediaStreamAdminRoutes
{
    /// <summary>Creates the route registration bound to the given minter.</summary>
    public static IReadOnlyList<HostAdminApiRouteRegistration> Build(
        IMediaStreamSessionMinter minter,
        ILogger<MintMediaStreamRouteHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(minter);
        ArgumentNullException.ThrowIfNull(logger);

        return
        [
            new HostAdminApiRouteRegistration(
                "POST",
                "calls/{callId}/media-streams",
                CommunicationPermissionKeys.CallsManage,
                new MintMediaStreamRouteHandler(minter, logger)),
        ];
    }
}
