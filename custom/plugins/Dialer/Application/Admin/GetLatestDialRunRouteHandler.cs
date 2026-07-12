using Callora.Host.PluginContracts.Application.Plugins;
using Callora.Plugins.Dialer.Application.Runs;

namespace Callora.Plugins.Dialer.Application.Admin;

public sealed class GetLatestDialRunRouteHandler(DialRunTracker tracker) : IHostAdminApiRouteHandler
{
    public ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AdminRequestWorkspace.TryGet(request, out var workspaceKey, out var error))
            return ValueTask.FromResult(error!);

        var snapshot = tracker.GetLatestRun(workspaceKey);
        if (snapshot is null)
        {
            return ValueTask.FromResult(new HostAdminApiResponse(404, new { message = "No dial run was started for this workspace." }));
        }

        return ValueTask.FromResult(new HostAdminApiResponse(200, snapshot));
    }
}
