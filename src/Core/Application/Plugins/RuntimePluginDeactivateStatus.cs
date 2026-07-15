namespace Callora.Core.Application.Plugins;

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
