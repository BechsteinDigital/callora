namespace Callora.Hosting.Application.Plugins;

/// <summary>
/// Lifecycle state of one runtime plugin.
/// </summary>
public enum RuntimePluginState
{
    Installed = 0,
    Active = 1,
    Inactive = 2,
}
