namespace Callora.Host.PluginContracts.Application.Plugins;

/// <summary>
/// Host view of plugin state.
/// </summary>
public enum HostPluginState
{
    Installed = 0,
    Active = 1,
    Inactive = 2,

    /// <summary>Activation failed; the plugin is installed but not serving.</summary>
    Faulted = 3,

    /// <summary>
    /// Deactivation stopped the plugin, but its assembly load context could not
    /// be released (still pinned); a host restart is required to fully unload it.
    /// </summary>
    UnloadFailed = 4,
}
