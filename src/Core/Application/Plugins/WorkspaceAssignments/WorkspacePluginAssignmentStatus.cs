namespace Callora.Core.Application.Plugins.WorkspaceAssignments;

public enum WorkspacePluginAssignmentStatus
{
    Ok = 0,
    WorkspaceNotFound = 1,
    PluginNotFound = 2,
    PluginInactive = 3,
    LifecycleRejected = 4,
    Forbidden = 5,
    PersistenceFailed = 6,
}
