namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Provides backend API and navigation extensions for the host Admin UI.
/// </summary>
public interface IHostAdminApiExtensionContributor
{
    /// <summary>
    /// Stable plugin identifier owning these extensions.
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// Permission keys contributed by the plugin.
    /// </summary>
    IReadOnlyList<string> PermissionKeys { get; }

    /// <summary>
    /// Declared API routes handled by the plugin.
    /// </summary>
    IReadOnlyList<HostAdminApiRouteRegistration> Routes { get; }

    /// <summary>
    /// Declared navigation entries for the Admin UI shell.
    /// </summary>
    IReadOnlyList<HostAdminNavigationItem> NavigationItems { get; }
}
