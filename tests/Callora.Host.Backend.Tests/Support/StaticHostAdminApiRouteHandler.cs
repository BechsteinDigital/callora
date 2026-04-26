using VoipHost.PluginContracts.Application.Plugins;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class StaticHostAdminApiRouteHandler(
    Func<HostAdminApiRequest, HostAdminApiResponse> handler) : IHostAdminApiRouteHandler
{
    public ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(handler(request));
    }
}
