using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Admin.SipAccounts;
using Callora.Plugin.Communication.Application.Voice;

namespace Callora.Plugin.Communication.Application.Admin.Numbers;

/// <summary>
/// The number plan's routes: what a workspace can be reached on, and how much of a line each number
/// may hold.
/// </summary>
/// <remarks>
/// Its own resource rather than a corner of the account routes, because a number is what an operator
/// thinks in — the account is where it happens to be configured. Reading needs
/// <see cref="CommunicationPermissionKeys.AccountsRead"/>, changing a quota
/// <see cref="CommunicationPermissionKeys.AccountsManage"/>: a quota is a line's capacity, and that is
/// the account's business.
/// </remarks>
public static class NumberAdminRoutes
{
    /// <summary>Creates the number-plan routes over the catalog, the accounts and the call history.</summary>
    /// <param name="reconciler">
    /// Applies a changed quota to the live ledger. Null in a deployment without a voice runtime, where
    /// the routes are pure persistence.
    /// </param>
    public static IReadOnlyList<HostAdminApiRouteRegistration> Build(
        IInboundNumberCatalog catalog,
        ISipAccountStore store,
        ICallHistory history,
        ISipAccountRuntimeReconciler? reconciler = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(history);

        return
        [
            new HostAdminApiRouteRegistration(
                "GET", "numbers", CommunicationPermissionKeys.AccountsRead,
                new GetNumberPlanRouteHandler(catalog, store, history)),
            new HostAdminApiRouteRegistration(
                "POST", "numbers/quota", CommunicationPermissionKeys.AccountsManage,
                new SetNumberQuotaRouteHandler(store, reconciler)),
        ];
    }
}
