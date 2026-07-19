namespace Callora.Core.Application.Workspaces.Events;

/// <summary>
/// Stable business-event names for workspace lifecycle changes. Consumers
/// (flows, webhooks, plugin listeners) subscribe by these dotted names.
/// </summary>
public static class WorkspaceEventTypes
{
    /// <summary>A new workspace was created.</summary>
    public const string Created = "workspace.created";

    /// <summary>An existing workspace was updated.</summary>
    public const string Updated = "workspace.updated";

    /// <summary>A workspace was deleted and its data purged.</summary>
    public const string Deleted = "workspace.deleted";
}
