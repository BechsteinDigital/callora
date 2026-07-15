namespace Callora.Core.Application.Plugins;

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
