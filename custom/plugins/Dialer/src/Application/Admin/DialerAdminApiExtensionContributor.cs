using Callora.Host.PluginContracts.Application.Plugins;
using Callora.Plugins.Dialer.Application.Numbers;
using Callora.Plugins.Dialer.Application.Runs;

namespace Callora.Plugins.Dialer.Application.Admin;

public sealed class DialerAdminApiExtensionContributor : IHostAdminApiExtensionContributor
{
    private readonly IReadOnlyList<HostAdminApiRouteRegistration> _routes;
    private readonly IReadOnlyList<HostAdminNavigationItem> _navigationItems;

    public DialerAdminApiExtensionContributor(IDialNumberStore numberStore, DialRunCoordinator runCoordinator)
    {
        ArgumentNullException.ThrowIfNull(numberStore);
        ArgumentNullException.ThrowIfNull(runCoordinator);

        _routes =
        [
            new HostAdminApiRouteRegistration("GET", "workspaces/{workspaceKey}/numbers", DialerPermissionKeys.NumbersRead, new ListNumbersRouteHandler(numberStore)),
            new HostAdminApiRouteRegistration("POST", "workspaces/{workspaceKey}/numbers", DialerPermissionKeys.NumbersManage, new AddNumberRouteHandler(numberStore)),
            new HostAdminApiRouteRegistration("DELETE", "workspaces/{workspaceKey}/numbers/{numberId}", DialerPermissionKeys.NumbersManage, new DeleteNumberRouteHandler(numberStore)),
            new HostAdminApiRouteRegistration("POST", "workspaces/{workspaceKey}/runs", DialerPermissionKeys.RunsStart, new StartDialRunRouteHandler(runCoordinator)),
            new HostAdminApiRouteRegistration("GET", "workspaces/{workspaceKey}/runs/latest", DialerPermissionKeys.RunsRead, new GetLatestDialRunRouteHandler(runCoordinator))
        ];

        _navigationItems =
        [
            new HostAdminNavigationItem(
                Id: "dialer-numbers",
                Label: "Dialer",
                To: "/extensions/dialer/numbers",
                Icon: "i-lucide-phone-outgoing",
                Order: 36,
                RequiredPermission: DialerPermissionKeys.NumbersRead)
        ];
    }

    public string PluginId => DialerPlugin.Id;

    public IReadOnlyList<string> PermissionKeys => DialerPermissionKeys.All;

    public IReadOnlyList<HostAdminApiRouteRegistration> Routes => _routes;

    public IReadOnlyList<HostAdminNavigationItem> NavigationItems => _navigationItems;
}
