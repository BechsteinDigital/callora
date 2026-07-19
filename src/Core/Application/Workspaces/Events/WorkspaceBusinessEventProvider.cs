using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Application.Workspaces.Events;

/// <summary>
/// Describes the workspace lifecycle and membership business events for discovery
/// (flow-builder, webhook UI): which events the host publishes and which fields they carry.
/// </summary>
public sealed class WorkspaceBusinessEventProvider : IBusinessEventProvider
{
    private static readonly IReadOnlyList<BusinessEventField> WorkspaceFields =
    [
        new("workspaceKey", BusinessEventFieldType.Text, "Workspace"),
        new("tenantKey", BusinessEventFieldType.Text, "Tenant"),
        new("displayName", BusinessEventFieldType.Text, "Display name"),
        new("workspaceType", BusinessEventFieldType.Text, "Type"),
        new("isActive", BusinessEventFieldType.Boolean, "Active"),
    ];

    private static readonly IReadOnlyList<BusinessEventField> MemberFields =
    [
        new("workspaceKey", BusinessEventFieldType.Text, "Workspace"),
        new("userId", BusinessEventFieldType.Text, "User"),
        new("role", BusinessEventFieldType.Text, "Role"),
        new("email", BusinessEventFieldType.Text, "Email"),
        new("displayName", BusinessEventFieldType.Text, "Display name"),
    ];

    private static readonly IReadOnlyList<BusinessEventField> MemberIdentityFields =
    [
        new("workspaceKey", BusinessEventFieldType.Text, "Workspace"),
        new("userId", BusinessEventFieldType.Text, "User"),
    ];

    /// <inheritdoc />
    public IReadOnlyList<BusinessEventDescriptor> GetDescriptors() =>
    [
        new(WorkspaceEventTypes.Created, "Workspace created", WorkspaceFields),
        new(WorkspaceEventTypes.Updated, "Workspace updated", WorkspaceFields),
        new(WorkspaceEventTypes.Deleted, "Workspace deleted", WorkspaceFields),
        new(WorkspaceMemberEventTypes.Assigned, "Workspace member assigned", MemberFields),
        new(WorkspaceMemberEventTypes.Removed, "Workspace member removed", MemberIdentityFields),
    ];
}
