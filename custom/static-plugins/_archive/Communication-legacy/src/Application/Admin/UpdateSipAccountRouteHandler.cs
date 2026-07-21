using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Channels;

namespace Callora.Plugin.Communication.Application.Admin;

public sealed class UpdateSipAccountRouteHandler(
    ISipAccountStore store,
    SipChannelManager channelManager) : IHostAdminApiRouteHandler
{
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AdminRequestWorkspace.TryGet(request, out var workspaceKey, out var error))
        {
            return error!;
        }

        if (!request.RouteValues.TryGetValue("sipAccountId", out var sipAccountId) || string.IsNullOrWhiteSpace(sipAccountId))
        {
            return new HostAdminApiResponse(400, new { message = "Route value 'sipAccountId' is required." });
        }

        if (!SipAccountRequestParser.TryParseUpsert(request.Body, out var payload, out var errorMessage))
        {
            return new HostAdminApiResponse(400, new { message = errorMessage ?? "Invalid payload." });
        }

        var updated = await store.UpdateAsync(workspaceKey, sipAccountId, payload!, cancellationToken).ConfigureAwait(false);
        if (updated is null)
        {
            return new HostAdminApiResponse(404, new { message = $"SIP account '{sipAccountId}' was not found." });
        }

        await channelManager.SynchronizeWorkspaceAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        return new HostAdminApiResponse(200, SipAccountMapper.ToApiModel(updated));
    }
}
