using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugin.Communication.Application.Admin;

/// <summary>
/// Operator Admin-API surface of the Communication plugin. v1 (walking skeleton) exposes a
/// status route plus navigation and declares the permission keys; account/line/call routes
/// are added onto this contributor with the domain/persistence baustein.
/// </summary>
public sealed class CommunicationAdminApiExtensionContributor : IHostAdminApiExtensionContributor
{
    private readonly IReadOnlyList<HostAdminApiRouteRegistration> _routes =
    [
        new HostAdminApiRouteRegistration(
            "GET",
            "status",
            CommunicationPermissionKeys.AccountsRead,
            new CommunicationStatusRouteHandler())
    ];

    private readonly IReadOnlyList<HostAdminNavigationItem> _navigationItems =
    [
        new HostAdminNavigationItem(
            Id: "communication",
            Label: "Communication",
            To: "/extensions/communication",
            Icon: "i-lucide-phone-call",
            Order: 35,
            RequiredPermission: CommunicationPermissionKeys.AccountsRead)
    ];

    /// <inheritdoc />
    public string PluginId => CommunicationPlugin.Id;

    /// <inheritdoc />
    public IReadOnlyList<string> PermissionKeys => CommunicationPermissionKeys.All;

    /// <inheritdoc />
    public IReadOnlyList<HostAdminApiRouteRegistration> Routes => _routes;

    /// <inheritdoc />
    public IReadOnlyList<HostAdminNavigationItem> NavigationItems => _navigationItems;
}
