using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// Builds the operator call-control Admin-API routes over the <see cref="ICallControlService"/>. Read
/// routes require <see cref="CommunicationPermissionKeys.CallsRead"/>; call-control (place/hangup)
/// requires <see cref="CommunicationPermissionKeys.CallsManage"/>. Registered by the plugin only when a
/// database is present (the service records call history). This is the out-of-process REST face of the
/// same primitive in-process plugins consume via DI.
/// </summary>
public static class CallAdminRoutes
{
    /// <summary>Creates the route registrations bound to the given call-control service.</summary>
    public static IReadOnlyList<HostAdminApiRouteRegistration> Build(ICallControlService callControl)
    {
        ArgumentNullException.ThrowIfNull(callControl);

        return
        [
            new HostAdminApiRouteRegistration(
                "GET", "calls", CommunicationPermissionKeys.CallsRead,
                new ListCallsRouteHandler(callControl)),
            new HostAdminApiRouteRegistration(
                "POST", "calls", CommunicationPermissionKeys.CallsManage,
                new PlaceCallRouteHandler(callControl)),
            new HostAdminApiRouteRegistration(
                "GET", "calls/{callId}", CommunicationPermissionKeys.CallsRead,
                new GetCallRouteHandler(callControl)),
            new HostAdminApiRouteRegistration(
                "POST", "calls/{callId}/hangup", CommunicationPermissionKeys.CallsManage,
                new HangupCallRouteHandler(callControl)),
        ];
    }
}
