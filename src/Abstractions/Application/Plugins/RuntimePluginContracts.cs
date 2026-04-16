namespace Callora.Modules.Abstractions.Application.Plugins;

/// <summary>
/// Lifecycle state of one runtime plugin.
/// </summary>
public enum RuntimePluginState
{
    Installed = 0,
    Active = 1,
    Inactive = 2,
}

/// <summary>
/// Runtime install status for plugin operations.
/// </summary>
public enum RuntimePluginInstallStatus
{
    Installed = 0,
    AlreadyInstalled = 1,
    InvalidPath = 2,
    EntryPointNotFound = 3,
    EntryPointInvalid = 4,
    Failed = 5,
}

/// <summary>
/// Runtime activate status for plugin operations.
/// </summary>
public enum RuntimePluginActivateStatus
{
    Activated = 0,
    AlreadyActive = 1,
    NotInstalled = 2,
    Failed = 3,
}

/// <summary>
/// Runtime deactivate status for plugin operations.
/// </summary>
public enum RuntimePluginDeactivateStatus
{
    Deactivated = 0,
    AlreadyInactive = 1,
    NotInstalled = 2,
    Failed = 3,
}

/// <summary>
/// Runtime uninstall status for plugin operations.
/// </summary>
public enum RuntimePluginUninstallStatus
{
    Uninstalled = 0,
    NotInstalled = 1,
    Failed = 2,
}

/// <summary>
/// Lightweight descriptor for one loaded plugin.
/// </summary>
public sealed record RuntimePluginDescriptor(
    string PluginId,
    string DisplayName,
    string AssemblyPath,
    string? EntryTypeName,
    RuntimePluginState State);

/// <summary>
/// Result of one plugin install operation.
/// </summary>
public sealed record RuntimePluginInstallResult(
    RuntimePluginInstallStatus Status,
    RuntimePluginDescriptor? Plugin,
    string? Message = null)
{
    public bool IsSuccess => Status is RuntimePluginInstallStatus.Installed or RuntimePluginInstallStatus.AlreadyInstalled;
}

/// <summary>
/// Result of one plugin activate operation.
/// </summary>
public sealed record RuntimePluginActivateResult(
    RuntimePluginActivateStatus Status,
    string PluginId,
    string? Message = null)
{
    public bool IsSuccess => Status is RuntimePluginActivateStatus.Activated or RuntimePluginActivateStatus.AlreadyActive;
}

/// <summary>
/// Result of one plugin deactivate operation.
/// </summary>
public sealed record RuntimePluginDeactivateResult(
    RuntimePluginDeactivateStatus Status,
    string PluginId,
    string? Message = null)
{
    public bool IsSuccess => Status is RuntimePluginDeactivateStatus.Deactivated or RuntimePluginDeactivateStatus.AlreadyInactive;
}

/// <summary>
/// Result of one plugin uninstall operation.
/// </summary>
public sealed record RuntimePluginUninstallResult(
    RuntimePluginUninstallStatus Status,
    string PluginId,
    string? Message = null)
{
    public bool IsSuccess => Status == RuntimePluginUninstallStatus.Uninstalled;
}
