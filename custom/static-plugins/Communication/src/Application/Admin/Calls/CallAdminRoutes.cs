using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Api.WebSocket;

namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// Builds the operator call-control Admin-API routes over the <see cref="ICallControlService"/>. Read
/// routes require <see cref="CommunicationPermissionKeys.CallsRead"/>; every route that changes a
/// call — place, accept, reject, DTMF, hang up — requires
/// <see cref="CommunicationPermissionKeys.CallsManage"/>. Registered by the plugin only when a
/// database is present (the service records call history). This is the out-of-process REST face of the
/// same primitive in-process plugins consume via DI.
/// </summary>
/// <remarks>
/// One permission covers all five control operations rather than a key per verb. They all act on the
/// same live conversation, and an operator who can hang a call up but not answer it is a distinction
/// no deployment has asked for; splitting the key later is additive, merging it back would not be.
/// </remarks>
public static class CallAdminRoutes
{
    /// <summary>Creates the route registrations bound to the given call-control service.</summary>
    /// <param name="callControl">The primitive every route delegates to.</param>
    /// <param name="eventStreamTickets">
    /// Mints tickets for the live call-event socket. Optional: without it the REST surface is
    /// complete but a client polls instead of following the stream.
    /// </param>
    public static IReadOnlyList<HostAdminApiRouteRegistration> Build(
        ICallControlService callControl,
        CallEventTicketStore? eventStreamTickets = null)
    {
        ArgumentNullException.ThrowIfNull(callControl);

        IReadOnlyList<HostAdminApiRouteRegistration> eventStreamRoutes = eventStreamTickets is null
            ? []
            :
            [
                new HostAdminApiRouteRegistration(
                    "POST", "calls/event-stream", CommunicationPermissionKeys.CallsRead,
                    new MintCallEventStreamRouteHandler(eventStreamTickets)),
            ];

        return
        [
            .. eventStreamRoutes,
            new HostAdminApiRouteRegistration(
                "GET", "calls", CommunicationPermissionKeys.CallsRead,
                new ListCallsRouteHandler(callControl)),
            new HostAdminApiRouteRegistration(
                "POST", "calls", CommunicationPermissionKeys.CallsManage,
                new PlaceCallRouteHandler(callControl)),
            // Declared before "calls/{callId}" so the literal segment is not swallowed by the
            // parameter route.
            new HostAdminApiRouteRegistration(
                "GET", "calls/active", CommunicationPermissionKeys.CallsRead,
                new ListActiveCallsRouteHandler(callControl)),
            new HostAdminApiRouteRegistration(
                "GET", "calls/{callId}", CommunicationPermissionKeys.CallsRead,
                new GetCallRouteHandler(callControl)),
            new HostAdminApiRouteRegistration(
                "POST", "calls/{callId}/accept", CommunicationPermissionKeys.CallsManage,
                new AcceptCallRouteHandler(callControl)),
            new HostAdminApiRouteRegistration(
                "POST", "calls/{callId}/reject", CommunicationPermissionKeys.CallsManage,
                new RejectCallRouteHandler(callControl)),
            new HostAdminApiRouteRegistration(
                "POST", "calls/{callId}/dtmf", CommunicationPermissionKeys.CallsManage,
                new SendDtmfRouteHandler(callControl)),
            new HostAdminApiRouteRegistration(
                "POST", "calls/{callId}/hangup", CommunicationPermissionKeys.CallsManage,
                new HangupCallRouteHandler(callControl)),
        ];
    }
}
