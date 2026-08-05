using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugin.Communication.Application.Admin;

/// <summary>
/// Operator Admin-API surface of the Communication plugin: the always-on status route plus, when the
/// deployment has persistence, the SIP-account management routes passed in at composition. Navigation
/// and permission keys are declared here.
/// </summary>
public sealed class CommunicationAdminApiExtensionContributor : IHostAdminApiExtensionContributor
{
    private readonly IReadOnlyList<HostAdminApiRouteRegistration> _routes;

    /// <summary>Creates the contributor with the status route plus the given account routes.</summary>
    /// <param name="accountRoutes">SIP-account and call-control routes, empty without persistence.</param>
    /// <param name="readinessProbe">Backs the status route's dependency aggregate (#112).</param>
    public CommunicationAdminApiExtensionContributor(
        IReadOnlyList<HostAdminApiRouteRegistration> accountRoutes,
        CommunicationReadinessProbe readinessProbe)
    {
        ArgumentNullException.ThrowIfNull(accountRoutes);
        ArgumentNullException.ThrowIfNull(readinessProbe);

        _routes =
        [
            // Plugin-wide health, deliberately not workspace-scoped: it reports
            // whether Communication itself is usable, which an operator must be
            // able to read even while no workspace is entitled (#109).
            new HostAdminApiRouteRegistration(
                "GET",
                "status",
                CommunicationPermissionKeys.AccountsRead,
                new CommunicationStatusRouteHandler(readinessProbe),
                HostAdminApiRouteScope.Global),
            .. accountRoutes,
        ];
    }

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
