using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Secrets.Contracts;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Voice;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>
/// Builds the operator SIP-account Admin-API routes over the persistence store. Read routes require
/// <see cref="CommunicationPermissionKeys.AccountsRead"/>; mutating routes require
/// <see cref="CommunicationPermissionKeys.AccountsManage"/>. Registered by the plugin only when both a
/// database and the plugin data protector are available (credentials cannot be handled otherwise).
/// </summary>
public static class SipAccountAdminRoutes
{
    /// <summary>Creates the route registrations bound to the given store, data protector and plugin id.</summary>
    /// <param name="reconciler">
    /// Brings the live runtime in line after every successful mutation (#110). Null in a
    /// deployment that operates no voice runtime, in which case the routes are pure
    /// persistence — which is then the truthful behaviour.
    /// </param>
    public static IReadOnlyList<HostAdminApiRouteRegistration> Build(
        ISipAccountStore store,
        IPluginDataProtector dataProtector,
        string pluginId,
        ISipAccountRuntimeReconciler? reconciler = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(dataProtector);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        return
        [
            new HostAdminApiRouteRegistration(
                "GET", "sip-accounts", CommunicationPermissionKeys.AccountsRead,
                new ListSipAccountsRouteHandler(store)),
            new HostAdminApiRouteRegistration(
                "GET", "sip-accounts/{accountId}", CommunicationPermissionKeys.AccountsRead,
                new GetSipAccountRouteHandler(store)),
            new HostAdminApiRouteRegistration(
                "POST", "sip-accounts", CommunicationPermissionKeys.AccountsManage,
                new CreateSipAccountRouteHandler(store, dataProtector, pluginId, reconciler)),
            new HostAdminApiRouteRegistration(
                "PUT", "sip-accounts/{accountId}", CommunicationPermissionKeys.AccountsManage,
                new UpdateSipAccountRouteHandler(store, dataProtector, pluginId, reconciler)),
            new HostAdminApiRouteRegistration(
                "POST", "sip-accounts/{accountId}/enable", CommunicationPermissionKeys.AccountsManage,
                new SetSipAccountEnabledRouteHandler(store, enabled: true, reconciler)),
            new HostAdminApiRouteRegistration(
                "POST", "sip-accounts/{accountId}/disable", CommunicationPermissionKeys.AccountsManage,
                new SetSipAccountEnabledRouteHandler(store, enabled: false, reconciler)),
            new HostAdminApiRouteRegistration(
                "DELETE", "sip-accounts/{accountId}", CommunicationPermissionKeys.AccountsManage,
                new DeleteSipAccountRouteHandler(store, reconciler)),
        ];
    }
}
