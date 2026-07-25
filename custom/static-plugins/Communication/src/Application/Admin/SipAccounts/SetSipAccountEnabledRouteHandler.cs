using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Accounts;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>
/// Handles <c>POST sip-accounts/{accountId}/enable</c> and <c>/disable</c> — toggles whether an account
/// is provisioned. One handler class, registered once per target state, so enable and disable share the
/// same guarded lookup and persistence.
/// </summary>
public sealed class SetSipAccountEnabledRouteHandler(ISipAccountStore store, bool enabled) : IHostAdminApiRouteHandler
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
        if (account is null)
        {
            return new HostAdminApiResponse(404, new { error = $"SIP account '{accountId}' was not found." });
        }

        if (enabled)
        {
            account.Enable();
        }
        else
        {
            account.Disable();
        }

        await store.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
        return new HostAdminApiResponse(200, SipAccountResponse.FromDomain(account));
    }
}
