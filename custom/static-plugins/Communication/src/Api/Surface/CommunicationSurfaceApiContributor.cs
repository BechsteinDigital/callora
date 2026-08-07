using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Accounts;

namespace Callora.Plugin.Communication.Api.Surface;

/// <summary>
/// The telephone routes a surface's own visitors may call, mounted under
/// <c>/surface-api/communication/…</c>.
/// </summary>
/// <remarks>
/// <para>Only the commands and the history. What is happening right now arrives as surface context —
/// the server publishes it, the runtime keeps the connection, and a block subscribes to a key. A
/// route for that would be a second way to learn the same thing, and the two would drift.</para>
/// <para>Every route is authenticated <em>and</em> claim-checked. The audience alone is not the bar
/// here: the workspace's calls are not the caller's own data, and a customer portal authenticates
/// just as truthfully as an agent desk.</para>
/// </remarks>
public sealed class CommunicationSurfaceApiContributor : IHostSurfaceApiContributor
{
    /// <summary>Route template for the workspace's recent calls, and for placing one.</summary>
    public const string CallsRouteTemplate = "calls";

    /// <summary>Route template for what is live right now.</summary>
    public const string ActiveCallsRouteTemplate = "calls/active";

    /// <summary>Route template for whether the phone can ring at all.</summary>
    public const string ChannelsRouteTemplate = "channels";

    private readonly IReadOnlyList<HostSurfaceApiRouteRegistration> _routes;

    /// <summary>Wires the surface call routes over call control and the history.</summary>
    /// <param name="accounts">
    /// The workspace's lines, for the status panel. Null in a deployment without persistence, where
    /// there are no configured lines to report on and the route is simply not offered.
    /// </param>
    public CommunicationSurfaceApiContributor(
        string pluginId,
        ICallControlService calls,
        ICallHistory history,
        ISipAccountStore? accounts = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(calls);
        ArgumentNullException.ThrowIfNull(history);

        PluginId = pluginId;
        _routes =
        [
            new HostSurfaceApiRouteRegistration("GET", CallsRouteTemplate, new SurfaceListCallsRouteHandler(history)),
            new HostSurfaceApiRouteRegistration("GET", ActiveCallsRouteTemplate, new SurfaceListActiveCallsRouteHandler(calls)),
            new HostSurfaceApiRouteRegistration("POST", CallsRouteTemplate, new SurfacePlaceCallRouteHandler(calls)),
            new HostSurfaceApiRouteRegistration(
                "POST", "calls/{callId}/accept", new SurfaceCallCommandRouteHandler(calls, SurfaceCallCommand.Accept)),
            new HostSurfaceApiRouteRegistration(
                "POST", "calls/{callId}/reject", new SurfaceCallCommandRouteHandler(calls, SurfaceCallCommand.Reject)),
            new HostSurfaceApiRouteRegistration(
                "POST", "calls/{callId}/hangup", new SurfaceCallCommandRouteHandler(calls, SurfaceCallCommand.Hangup)),
            new HostSurfaceApiRouteRegistration(
                "POST", "calls/{callId}/dtmf", new SurfaceSendDtmfRouteHandler(calls)),
            .. accounts is null
                ? Array.Empty<HostSurfaceApiRouteRegistration>()
                : new[]
                {
                    new HostSurfaceApiRouteRegistration(
                        "GET", ChannelsRouteTemplate, new SurfaceListChannelsRouteHandler(accounts)),
                },
        ];
    }

    /// <inheritdoc />
    public string PluginId { get; }

    /// <inheritdoc />
    public IReadOnlyList<HostSurfaceApiRouteRegistration> Routes => _routes;
}
