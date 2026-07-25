using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Accounts;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>Handles <c>GET sip-accounts/{accountId}</c> — one account of the caller's workspace, or 404.</summary>
public sealed class GetSipAccountRouteHandler(ISipAccountStore store) : IHostAdminApiRouteHandler
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

        var accountId = request.RouteValues.TryGetValue("accountId", out var value) ? value : string.Empty;
        var account = await store.GetAsync(workspaceKey, accountId, cancellationToken).ConfigureAwait(false);

        return account is null
            ? new HostAdminApiResponse(404, new { error = $"SIP account '{accountId}' was not found." })
            : new HostAdminApiResponse(200, SipAccountResponse.FromDomain(account));
    }
}
