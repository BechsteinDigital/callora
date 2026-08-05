using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.WebRtc;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Application.Admin.WebRtc;

/// <summary>
/// Builds the WebRTC session Admin-API route. Registered only when the deployment enables WebRTC —
/// a route that can never succeed is worse than a missing one, because it reads as a capability.
/// </summary>
public static class WebRtcAdminRoutes
{
    /// <summary>Creates the route registration bound to the given minter and ICE configuration.</summary>
    public static IReadOnlyList<HostAdminApiRouteRegistration> Build(
        IWebRtcSessionMinter minter,
        CommunicationReadinessProbe readinessProbe,
        IceConfigurationOptions iceOptions,
        TimeProvider timeProvider,
        ILogger<MintWebRtcSessionRouteHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(minter);
        ArgumentNullException.ThrowIfNull(readinessProbe);
        ArgumentNullException.ThrowIfNull(iceOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        return
        [
            new HostAdminApiRouteRegistration(
                "POST",
                "webrtc/sessions",
                CommunicationPermissionKeys.CallsManage,
                new MintWebRtcSessionRouteHandler(minter, readinessProbe, iceOptions, timeProvider, logger)),
        ];
    }
}
