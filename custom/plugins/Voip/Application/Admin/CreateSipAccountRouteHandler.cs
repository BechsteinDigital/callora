using VoipHost.PluginContracts.Application.Plugins;

namespace Callora.Plugins.Voip.Application.Admin;

public sealed class CreateSipAccountRouteHandler(ISipAccountStore store) : IHostAdminApiRouteHandler
{
    public ValueTask<HostAdminApiResponse> HandleAsync(HostAdminApiRequest request, CancellationToken cancellationToken = default)
    {
        if (!SipAccountRequestParser.TryParseUpsert(request.Body, out var payload, out var errorMessage))
        {
            return ValueTask.FromResult(new HostAdminApiResponse(400, new { message = errorMessage ?? "Invalid payload." }));
        }

        try
        {
            var created = store.Create(payload!);
            return ValueTask.FromResult(new HostAdminApiResponse(201, SipAccountMapper.ToApiModel(created)));
        }
        catch (InvalidOperationException ex)
        {
            return ValueTask.FromResult(new HostAdminApiResponse(409, new { message = ex.Message }));
        }
    }
}
