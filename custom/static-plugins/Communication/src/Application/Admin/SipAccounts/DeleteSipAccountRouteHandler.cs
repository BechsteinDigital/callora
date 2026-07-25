using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Accounts;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>Handles <c>DELETE sip-accounts/{accountId}</c> — removes an account of the caller's workspace.</summary>
public sealed class DeleteSipAccountRouteHandler(ISipAccountStore store) : IHostAdminApiRouteHandler
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
        var deleted = await store.DeleteAsync(workspaceKey, accountId, cancellationToken).ConfigureAwait(false);

        return deleted
            ? new HostAdminApiResponse(204)
            : new HostAdminApiResponse(404, new { error = $"SIP account '{accountId}' was not found." });
    }
}
