namespace Callora.Core.Application.Plugins;

/// <summary>
/// Result of one plugin uninstall operation.
/// </summary>
public sealed record RuntimePluginUninstallResult(
    RuntimePluginUninstallStatus Status,
    string PluginId,
    string? Message = null)
{
    /// <summary>
    /// Indicates whether the plugin is uninstalled after the operation.
    /// </summary>
    public bool IsSuccess => Status == RuntimePluginUninstallStatus.Uninstalled;
}
