namespace Callora.Core.Application.Plugins;

/// <summary>
/// Lifecycle state of one runtime plugin, including explicit failure
/// states (PLAT-255): a plugin whose activation or teardown failed is
/// visibly faulted instead of silently "inactive".
/// </summary>
public enum RuntimePluginState
{
    /// <summary>Installed but never activated.</summary>
    Installed = 0,

    /// <summary>Started and serving exports.</summary>
    Active = 1,

    /// <summary>Deactivated cleanly; can be activated again.</summary>
    Inactive = 2,

    /// <summary>Activation failed; the plugin is not serving.</summary>
    Faulted = 3,

    /// <summary>
    /// StopAsync or unload failed during deactivation — exports are
    /// removed, but resources may still be pinned until a host restart.
    /// </summary>
    UnloadFailed = 4
}
