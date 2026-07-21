using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugin.Communication.Application.Admin;

/// <summary>
/// Handles <c>GET status</c> — a lightweight readiness probe that proves the plugin's
/// operator surface is reachable. Real account/line/call routes arrive with the
/// domain/persistence baustein.
/// </summary>
public sealed class CommunicationStatusRouteHandler : IHostAdminApiRouteHandler
{
    /// <inheritdoc />
    public ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = new CommunicationStatus(CommunicationPlugin.Id, "ok");
        return ValueTask.FromResult(new HostAdminApiResponse(200, payload));
    }
}
