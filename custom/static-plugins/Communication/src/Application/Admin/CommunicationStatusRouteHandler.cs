using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugin.Communication.Application.Admin;

/// <summary>
/// Handles <c>GET status</c>. Answers whether Communication can currently serve calls by
/// asking the dependencies that gate one, rather than the constant <c>ok</c> it returned
/// before (#112).
/// <para>
/// The HTTP status carries the verdict so a monitor does not have to parse the body:
/// <c>200</c> while calls are possible (ready or degraded), <c>503</c> when they are not. The
/// body always lists every dependency, so an operator can see which one is at fault.
/// </para>
/// <para>
/// This is readiness. Host liveness is a separate probe and stays independent of external
/// provider availability, so a carrier outage never gets a healthy process restarted.
/// </para>
/// </summary>
public sealed class CommunicationStatusRouteHandler(CommunicationReadinessProbe probe) : IHostAdminApiRouteHandler
{
    /// <inheritdoc />
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var status = await probe.ProbeAsync(cancellationToken).ConfigureAwait(false);
        var statusCode = status.Status == CommunicationReadiness.Unavailable ? 503 : 200;
        return new HostAdminApiResponse(statusCode, status);
    }
}
