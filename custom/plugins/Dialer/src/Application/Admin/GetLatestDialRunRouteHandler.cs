using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugins.Dialer.Application.Runs;

namespace Callora.Plugins.Dialer.Application.Admin;

public sealed class GetLatestDialRunRouteHandler(DialRunCoordinator coordinator) : IHostAdminApiRouteHandler
{
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AdminRequestWorkspace.TryGet(request, out var workspaceKey, out var error))
            return error!;

        var snapshot = await coordinator.GetLatestRunAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return new HostAdminApiResponse(404, new { message = "No dial run was started for this workspace." });
        }

        return new HostAdminApiResponse(200, snapshot);
    }
}
