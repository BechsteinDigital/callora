namespace Callora.Core.Application.Plugins;

/// <summary>
/// Deployment tier of a plugin (REV2 §3). System/Foundation plugins are bundled
/// with the distribution (custom/static-plugins) and load before Application
/// plugins; Application plugins (custom/plugins) are installable, updatable and
/// workspace-activatable products.
/// </summary>
public enum PluginTier
{
    Application = 0,
    System = 1,
}
