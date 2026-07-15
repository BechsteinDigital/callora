using Callora.Host.PluginContracts.Application.Plugins;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Channels;

namespace Callora.Plugin.Communication.Application.Admin;

public sealed class VoipAdminApiExtensionContributor : IHostAdminApiExtensionContributor
{
    private readonly IReadOnlyList<HostAdminApiRouteRegistration> _routes;
    private readonly IReadOnlyList<HostAdminNavigationItem> _navigationItems;

    public VoipAdminApiExtensionContributor(ISipAccountStore store, SipChannelManager channelManager)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(channelManager);

        _routes =
        [
            new HostAdminApiRouteRegistration("GET", "workspaces/{workspaceKey}/sip-accounts", VoipPermissionKeys.SipAccountRead, new ListSipAccountsRouteHandler(store)),
            new HostAdminApiRouteRegistration("GET", "workspaces/{workspaceKey}/sip-accounts/{sipAccountId}", VoipPermissionKeys.SipAccountRead, new GetSipAccountRouteHandler(store)),
            new HostAdminApiRouteRegistration("POST", "workspaces/{workspaceKey}/sip-accounts", VoipPermissionKeys.SipAccountCreate, new CreateSipAccountRouteHandler(store, channelManager)),
            new HostAdminApiRouteRegistration("PUT", "workspaces/{workspaceKey}/sip-accounts/{sipAccountId}", VoipPermissionKeys.SipAccountUpdate, new UpdateSipAccountRouteHandler(store, channelManager)),
            new HostAdminApiRouteRegistration("DELETE", "workspaces/{workspaceKey}/sip-accounts/{sipAccountId}", VoipPermissionKeys.SipAccountDelete, new DeleteSipAccountRouteHandler(store, channelManager))
        ];

        _navigationItems =
        [
            new HostAdminNavigationItem(
                Id: "voip-sip-accounts",
                Label: "SIP Accounts",
                To: "/extensions/voip/sip-accounts",
                Icon: "i-lucide-phone-call",
                Order: 35,
                RequiredPermission: VoipPermissionKeys.SipAccountRead)
        ];
    }

    public string PluginId => CommunicationPlugin.Id;

    public IReadOnlyList<string> PermissionKeys => VoipPermissionKeys.All;

    public IReadOnlyList<HostAdminApiRouteRegistration> Routes => _routes;

    public IReadOnlyList<HostAdminNavigationItem> NavigationItems => _navigationItems;
}
