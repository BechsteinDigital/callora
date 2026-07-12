using Callora.Host.PluginContracts.Application.Plugins;
using Callora.Plugins.Voip.Application.Accounts;
using Callora.Plugins.Voip.Application.Channels;

namespace Callora.Plugins.Voip.Application.Admin;

public sealed class DeleteSipAccountRouteHandler(
    ISipAccountStore store,
    SipChannelManager channelManager) : IHostAdminApiRouteHandler
{
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AdminRequestWorkspace.TryGet(request, out var workspaceKey, out var error))
            return error!;

        if (!request.RouteValues.TryGetValue("sipAccountId", out var sipAccountId) || string.IsNullOrWhiteSpace(sipAccountId))
        {
            return new HostAdminApiResponse(400, new { message = "Route value 'sipAccountId' is required." });
        }

        var deleted = await store.DeleteAsync(workspaceKey, sipAccountId, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return new HostAdminApiResponse(404, new { message = $"SIP account '{sipAccountId}' was not found." });
        }

        await channelManager.SynchronizeWorkspaceAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        return new HostAdminApiResponse(204);
    }
}
