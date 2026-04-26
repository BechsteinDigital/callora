using VoipHost.PluginContracts.Application.Plugins;

namespace Callora.Plugins.Voip.Application.Admin;

public sealed class ListSipAccountsRouteHandler(ISipAccountStore store) : IHostAdminApiRouteHandler
{
    public ValueTask<HostAdminApiResponse> HandleAsync(HostAdminApiRequest request, CancellationToken cancellationToken = default)
    {
        var payload = store.List().Select(SipAccountMapper.ToApiModel).ToArray();
        return ValueTask.FromResult(new HostAdminApiResponse(StatusCode: 200, Payload: payload));
    }
}
