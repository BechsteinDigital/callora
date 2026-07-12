namespace Callora.Host.PluginContracts.Application.Plugins;

/// <summary>
/// Exposes code-first extension registrations from one active plugin.
/// </summary>
public interface IHostPluginExtensionContributor
{
    /// <summary>
    /// Stable plugin identifier owning these registrations.
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// Declared capabilities/scopes used for extension authorization checks.
    /// </summary>
    IReadOnlyList<string> Capabilities { get; }

    /// <summary>
    /// Returns extension registrations for this plugin.
    /// </summary>
    IReadOnlyList<HostPluginExtensionRegistration> GetRegistrations();
}
