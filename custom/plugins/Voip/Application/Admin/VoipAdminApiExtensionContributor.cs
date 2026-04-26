using VoipHost.PluginContracts.Application.Plugins;

namespace Callora.Plugins.Voip.Application.Admin;

public sealed class VoipAdminApiExtensionContributor : IHostAdminApiExtensionContributor
{
    private readonly IReadOnlyList<HostAdminApiRouteRegistration> _routes;
    private readonly IReadOnlyList<HostAdminNavigationItem> _navigationItems;

    public VoipAdminApiExtensionContributor()
    {
        var store = new InMemorySipAccountStore();

        _routes =
        [
            new HostAdminApiRouteRegistration("GET", "sip-accounts", VoipPermissionKeys.SipAccountRead, new ListSipAccountsRouteHandler(store)),
            new HostAdminApiRouteRegistration("GET", "sip-accounts/{sipAccountId}", VoipPermissionKeys.SipAccountRead, new GetSipAccountRouteHandler(store)),
            new HostAdminApiRouteRegistration("POST", "sip-accounts", VoipPermissionKeys.SipAccountCreate, new CreateSipAccountRouteHandler(store)),
            new HostAdminApiRouteRegistration("PUT", "sip-accounts/{sipAccountId}", VoipPermissionKeys.SipAccountUpdate, new UpdateSipAccountRouteHandler(store)),
            new HostAdminApiRouteRegistration("DELETE", "sip-accounts/{sipAccountId}", VoipPermissionKeys.SipAccountDelete, new DeleteSipAccountRouteHandler(store))
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

    public string PluginId => VoipPlugin.Id;

    public IReadOnlyList<string> PermissionKeys => VoipPermissionKeys.All;

    public IReadOnlyList<HostAdminApiRouteRegistration> Routes => _routes;

    public IReadOnlyList<HostAdminNavigationItem> NavigationItems => _navigationItems;
}
