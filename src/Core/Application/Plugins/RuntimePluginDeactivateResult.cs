namespace Callora.Core.Application.Plugins;

/// <summary>
/// Result of one plugin deactivate operation.
/// </summary>
public sealed record RuntimePluginDeactivateResult(
    RuntimePluginDeactivateStatus Status,
    string PluginId,
    string? Message = null)
{
    /// <summary>
    /// Indicates whether the plugin is inactive after the operation.
    /// </summary>
    public bool IsSuccess => Status is RuntimePluginDeactivateStatus.Deactivated or RuntimePluginDeactivateStatus.AlreadyInactive;
}
