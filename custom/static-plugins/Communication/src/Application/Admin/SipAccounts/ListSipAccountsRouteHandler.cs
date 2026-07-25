using System.Linq;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Accounts;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>Handles <c>GET sip-accounts</c> — lists the caller's workspace's SIP accounts.</summary>
public sealed class ListSipAccountsRouteHandler(ISipAccountStore store) : IHostAdminApiRouteHandler
{
    /// <inheritdoc />
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SipAccountAdminScope.TryResolve(request, out var workspaceKey, out var error))
        {
            return error!;
        }

        var accounts = await store.ListAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        return new HostAdminApiResponse(200, accounts.Select(SipAccountResponse.FromDomain).ToArray());
    }
}
