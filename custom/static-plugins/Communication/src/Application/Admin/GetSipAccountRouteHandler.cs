using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Accounts;

namespace Callora.Plugin.Communication.Application.Admin;

public sealed class GetSipAccountRouteHandler(ISipAccountStore store) : IHostAdminApiRouteHandler
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

        var entry = await store.GetAsync(workspaceKey, sipAccountId, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return new HostAdminApiResponse(404, new { message = $"SIP account '{sipAccountId}' was not found." });
        }

        return new HostAdminApiResponse(200, SipAccountMapper.ToApiModel(entry));
    }
}
