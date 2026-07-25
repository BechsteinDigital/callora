using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Secrets.Contracts;
using Callora.Plugin.Communication.Application.Accounts;

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
    public static IReadOnlyList<HostAdminApiRouteRegistration> Build(
        ISipAccountStore store,
        IPluginDataProtector dataProtector,
        string pluginId)
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
                new CreateSipAccountRouteHandler(store, dataProtector, pluginId)),
            new HostAdminApiRouteRegistration(
                "POST", "sip-accounts/{accountId}/enable", CommunicationPermissionKeys.AccountsManage,
                new SetSipAccountEnabledRouteHandler(store, enabled: true)),
            new HostAdminApiRouteRegistration(
                "POST", "sip-accounts/{accountId}/disable", CommunicationPermissionKeys.AccountsManage,
                new SetSipAccountEnabledRouteHandler(store, enabled: false)),
            new HostAdminApiRouteRegistration(
                "DELETE", "sip-accounts/{accountId}", CommunicationPermissionKeys.AccountsManage,
                new DeleteSipAccountRouteHandler(store)),
        ];
    }
}
