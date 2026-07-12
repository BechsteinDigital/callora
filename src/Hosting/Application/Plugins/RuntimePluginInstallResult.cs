namespace Callora.Hosting.Application.Plugins;

/// <summary>
/// Result of one plugin install operation.
/// </summary>
public sealed record RuntimePluginInstallResult(
    RuntimePluginInstallStatus Status,
    RuntimePluginDescriptor? Plugin,
    string? Message = null)
{
    /// <summary>
    /// Indicates whether the install operation ended in a usable installed state.
    /// </summary>
    public bool IsSuccess => Status is RuntimePluginInstallStatus.Installed or RuntimePluginInstallStatus.AlreadyInstalled;
}
