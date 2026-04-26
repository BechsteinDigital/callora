using VoipHost.PluginContracts.Application.Plugins;

namespace Callora.Plugins.Voip.Application.Admin;

public sealed class UpdateSipAccountRouteHandler(ISipAccountStore store) : IHostAdminApiRouteHandler
{
    public ValueTask<HostAdminApiResponse> HandleAsync(HostAdminApiRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.RouteValues.TryGetValue("sipAccountId", out var sipAccountId) || string.IsNullOrWhiteSpace(sipAccountId))
        {
            return ValueTask.FromResult(new HostAdminApiResponse(400, new { message = "Route value 'sipAccountId' is required." }));
        }

        if (!SipAccountRequestParser.TryParseUpsert(request.Body, out var payload, out var errorMessage))
        {
            return ValueTask.FromResult(new HostAdminApiResponse(400, new { message = errorMessage ?? "Invalid payload." }));
        }

        var updated = store.Update(sipAccountId, payload!);
        if (updated is null)
        {
            return ValueTask.FromResult(new HostAdminApiResponse(404, new { message = $"SIP account '{sipAccountId}' was not found." }));
        }

        return ValueTask.FromResult(new HostAdminApiResponse(200, SipAccountMapper.ToApiModel(updated)));
    }
}
