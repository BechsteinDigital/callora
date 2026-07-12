namespace Callora.Host.PluginContracts.Application.Plugins;

/// <summary>
/// Lifecycle operation kind.
/// </summary>
public enum HostPluginOperation
{
    Install = 0,
    Activate = 1,
    Deactivate = 2,
    Uninstall = 3,
}
