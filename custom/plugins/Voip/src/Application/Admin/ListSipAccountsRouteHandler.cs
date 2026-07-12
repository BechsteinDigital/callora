using Callora.Host.PluginContracts.Application.Plugins;
using Callora.Plugins.Voip.Application.Accounts;

namespace Callora.Plugins.Voip.Application.Admin;

public sealed class ListSipAccountsRouteHandler(ISipAccountStore store) : IHostAdminApiRouteHandler
{
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AdminRequestWorkspace.TryGet(request, out var workspaceKey, out var error))
            return error!;

        var accounts = await store.ListAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        return new HostAdminApiResponse(200, accounts.Select(SipAccountMapper.ToApiModel).ToArray());
    }
}
