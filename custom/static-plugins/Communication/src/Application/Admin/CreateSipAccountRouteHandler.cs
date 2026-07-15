using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Channels;

namespace Callora.Plugin.Communication.Application.Admin;

public sealed class CreateSipAccountRouteHandler(
    ISipAccountStore store,
    SipChannelManager channelManager) : IHostAdminApiRouteHandler
{
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AdminRequestWorkspace.TryGet(request, out var workspaceKey, out var error))
            return error!;

        if (!SipAccountRequestParser.TryParseUpsert(request.Body, out var payload, out var errorMessage))
        {
            return new HostAdminApiResponse(400, new { message = errorMessage ?? "Invalid payload." });
        }

        try
        {
            var created = await store.CreateAsync(workspaceKey, payload!, cancellationToken).ConfigureAwait(false);
            await channelManager.SynchronizeWorkspaceAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            return new HostAdminApiResponse(201, SipAccountMapper.ToApiModel(created));
        }
        catch (InvalidOperationException ex)
        {
            return new HostAdminApiResponse(409, new { message = ex.Message });
        }
    }
}
