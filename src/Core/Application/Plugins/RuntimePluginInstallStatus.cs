namespace Callora.Core.Application.Plugins;

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
