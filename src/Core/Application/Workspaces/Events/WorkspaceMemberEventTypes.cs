namespace Callora.Core.Application.Workspaces.Events;

/// <summary>
/// Stable business-event names for workspace membership changes. Consumers
/// (flows, webhooks, plugin listeners) subscribe by these dotted names.
/// </summary>
public static class WorkspaceMemberEventTypes
{
    /// <summary>A member was assigned to a workspace or had its role changed.</summary>
    public const string Assigned = "workspace.member-assigned";

    /// <summary>A member was removed from a workspace.</summary>
    public const string Removed = "workspace.member-removed";
}
