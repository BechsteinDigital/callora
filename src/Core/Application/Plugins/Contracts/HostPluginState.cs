namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Host view of plugin state.
/// </summary>
public enum HostPluginState
{
    /// <summary>Assembly loaded and recorded, but not yet serving.</summary>
    Installed = 0,

    /// <summary>Started and serving its exports to the host.</summary>
    Active = 1,

    /// <summary>Previously active, now stopped but still installed.</summary>
    Inactive = 2,

    /// <summary>Activation failed; the plugin is installed but not serving.</summary>
    Faulted = 3,

    /// <summary>
    /// Deactivation stopped the plugin, but its assembly load context could not
    /// be released (still pinned); a host restart is required to fully unload it.
    /// </summary>
    UnloadFailed = 4,
}
