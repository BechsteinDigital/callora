namespace Callora.Hosting.Application.Options;

/// <summary>
/// Options for plugin hosting in host applications.
/// </summary>
public sealed class CalloraHostingOptions
{
    /// <summary>
    /// Enables automatic plugin discovery/load from <see cref="PluginDirectory"/>.
    /// </summary>
    public bool AutoLoadPlugins { get; set; }

    /// <summary>
    /// Directory scanned for runtime plugins when <see cref="AutoLoadPlugins"/> is enabled.
    /// </summary>
    public string PluginDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "custom", "plugins");

    /// <summary>
    /// Automatically activates installed plugins marked as active in runtime state.
    /// </summary>
    public bool AutoActivateInstalledPlugins { get; set; } = true;
}
