using VoipHost.PluginContracts.Application.Plugins;

namespace Callora.Plugins.Voip.Application.Admin;

public sealed class DeleteSipAccountRouteHandler(ISipAccountStore store) : IHostAdminApiRouteHandler
{
    public ValueTask<HostAdminApiResponse> HandleAsync(HostAdminApiRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.RouteValues.TryGetValue("sipAccountId", out var sipAccountId) || string.IsNullOrWhiteSpace(sipAccountId))
        {
            return ValueTask.FromResult(new HostAdminApiResponse(400, new { message = "Route value 'sipAccountId' is required." }));
        }

        var deleted = store.Delete(sipAccountId);
        if (!deleted)
        {
            return ValueTask.FromResult(new HostAdminApiResponse(404, new { message = $"SIP account '{sipAccountId}' was not found." }));
        }

        return ValueTask.FromResult(new HostAdminApiResponse(204));
    }
}
