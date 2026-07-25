namespace Callora.Core.Application.Options;

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
    /// Directory scanned for Application-tier runtime plugins when
    /// <see cref="AutoLoadPlugins"/> is enabled.
    /// </summary>
    public string PluginDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "custom", "plugins");

    /// <summary>
    /// Directory scanned for bundled System/Foundation-tier plugins. Scanned
    /// before <see cref="PluginDirectory"/>, so foundation plugins load first.
    /// </summary>
    public string StaticPluginDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "custom", "static-plugins");

    /// <summary>
    /// Automatically activates installed plugins marked as active in runtime state.
    /// </summary>
    public bool AutoActivateInstalledPlugins { get; set; } = true;

    /// <summary>
    /// Grace period the runtime-capability registry waits before a health-derived capability loss
    /// takes effect, damping transient flaps (a channel that briefly reconnects should not deactivate
    /// dependents). Return to satisfied is always immediate. <see cref="TimeSpan.Zero"/> flips a loss
    /// immediately (no damping).
    /// </summary>
    public TimeSpan RuntimeCapabilityGracePeriod { get; set; } = TimeSpan.FromSeconds(30);
}
