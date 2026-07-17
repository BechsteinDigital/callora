namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Lifecycle operation kind.
/// </summary>
public enum HostPluginOperation
{
    /// <summary>Loading the plugin assembly and recording it as installed.</summary>
    Install = 0,

    /// <summary>Starting an installed plugin so its exports serve.</summary>
    Activate = 1,

    /// <summary>Stopping an active plugin and withdrawing its exports.</summary>
    Deactivate = 2,

    /// <summary>Removing a plugin from the known set.</summary>
    Uninstall = 3,
}
