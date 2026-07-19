using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Application.Workspaces.Events;

/// <summary>
/// A workspace lifecycle business event (created/updated/deleted), published to the
/// business-event bus so flows, webhooks and plugin listeners react to workspace
/// provisioning through the same generic mechanism as any other event (PLAT-270).
/// </summary>
public sealed class WorkspaceBusinessEvent : IBusinessEvent
{
    private readonly WorkspaceSnapshot _workspace;

    private WorkspaceBusinessEvent(string eventName, WorkspaceSnapshot workspace)
    {
        EventName = eventName;
        _workspace = workspace;
    }

    /// <inheritdoc />
    public string EventName { get; }

    /// <inheritdoc />
    public string? WorkspaceKey => _workspace.WorkspaceKey;

    /// <summary>
    /// Builds a created or updated event from an upsert result. The two are told apart by
    /// the snapshot's timestamps: a freshly inserted workspace has equal created/updated
    /// stamps, an updated one has a later update stamp.
    /// </summary>
    public static WorkspaceBusinessEvent ForUpsert(WorkspaceSnapshot workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var eventName = workspace.CreatedAtUtc == workspace.UpdatedAtUtc
            ? WorkspaceEventTypes.Created
            : WorkspaceEventTypes.Updated;
        return new WorkspaceBusinessEvent(eventName, workspace);
    }

    /// <summary>Builds a deleted event for a purged workspace.</summary>
    public static WorkspaceBusinessEvent ForDeletion(WorkspaceSnapshot workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return new WorkspaceBusinessEvent(WorkspaceEventTypes.Deleted, workspace);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ToEventData() => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["workspaceKey"] = _workspace.WorkspaceKey,
        ["tenantKey"] = _workspace.TenantKey,
        ["displayName"] = _workspace.DisplayName,
        ["workspaceType"] = _workspace.WorkspaceType,
        ["isActive"] = _workspace.IsActive ? "true" : "false",
    };
}
