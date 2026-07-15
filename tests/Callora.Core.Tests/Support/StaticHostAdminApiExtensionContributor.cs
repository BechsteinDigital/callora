using Callora.Host.PluginContracts.Application.Plugins;

namespace Callora.Core.Tests.Support;

internal sealed class StaticHostAdminApiExtensionContributor : IHostAdminApiExtensionContributor
{
    public required string PluginId { get; init; }

    public required IReadOnlyList<string> PermissionKeys { get; init; }

    public required IReadOnlyList<HostAdminApiRouteRegistration> Routes { get; init; }

    public required IReadOnlyList<HostAdminNavigationItem> NavigationItems { get; init; }
}
