using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Calls;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// Contributes the plugin's call-event WebSocket endpoint under the host's reserved prefix:
/// <c>/ws/communication/calls/{connectToken}</c>. The ticket is validated (and consumed) by
/// <see cref="CallEventConnectTokenAuthorizer"/> before the socket is accepted;
/// <see cref="CallEventWebSocketHandler"/> then streams the workspace's call transitions. The third
/// sibling on the same host WebSocket seam, next to media and WebRTC signalling.
/// </summary>
public sealed class CommunicationCallEventContributor : IHostWebSocketEndpointContributor
{
    /// <summary>Route template relative to the plugin prefix (<c>/ws/communication/…</c>).</summary>
    public const string RouteTemplate = "calls/{connectToken}";

    private readonly IReadOnlyList<HostWebSocketRouteRegistration> _routes;

    /// <summary>Wires the call-event route with its ticket authorizer and stream handler.</summary>
    public CommunicationCallEventContributor(
        CallEventTicketStore tickets,
        CallEventBroadcaster broadcaster,
        ILogger<CallEventWebSocketHandler> handlerLogger)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        ArgumentNullException.ThrowIfNull(broadcaster);
        ArgumentNullException.ThrowIfNull(handlerLogger);

        _routes =
        [
            new HostWebSocketRouteRegistration(
                RouteTemplate,
                new CallEventConnectTokenAuthorizer(tickets),
                new CallEventWebSocketHandler(broadcaster, handlerLogger))
        ];
    }

    /// <inheritdoc />
    public string PluginId => CommunicationPlugin.Id;

    /// <inheritdoc />
    public IReadOnlyList<HostWebSocketRouteRegistration> WebSocketRoutes => _routes;
}
