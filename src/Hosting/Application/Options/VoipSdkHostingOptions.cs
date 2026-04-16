namespace Callora.Hosting.Application.Options;

/// <summary>
/// Options for module bootstrapping in host applications.
/// </summary>
public sealed class CalloraHostingOptions
{
    /// <summary>
    /// Enables automatic module bootstrap on host startup.
    /// </summary>
    public bool AutoBootstrapModules { get; set; } = true;

    /// <summary>
    /// Enables automatic plugin discovery/load from <see cref="PluginDirectory"/>.
    /// </summary>
    public bool AutoLoadPlugins { get; set; }

    /// <summary>
    /// Directory scanned for runtime plugins when <see cref="AutoLoadPlugins"/> is enabled.
    /// </summary>
    public string PluginDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "plugins");

    /// <summary>
    /// Persisted plugin registry file path (install + activation state).
    /// </summary>
    public string PluginRegistryFilePath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "plugins", "registry.json");

    /// <summary>
    /// Automatically activates installed plugins marked as active in the persisted registry.
    /// </summary>
    public bool AutoActivateInstalledPlugins { get; set; } = true;
}
