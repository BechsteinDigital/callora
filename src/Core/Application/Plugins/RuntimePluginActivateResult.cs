namespace Callora.Core.Application.Plugins;

/// <summary>
/// Result of one plugin activate operation.
/// </summary>
public sealed record RuntimePluginActivateResult(
    RuntimePluginActivateStatus Status,
    string PluginId,
    string? Message = null)
{
    /// <summary>
    /// Indicates whether the plugin is active after the operation.
    /// </summary>
    public bool IsSuccess => Status is RuntimePluginActivateStatus.Activated or RuntimePluginActivateStatus.AlreadyActive;
}
