using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Application.Workspaces.Events;

/// <summary>
/// A workspace membership business event (assigned/removed), published to the
/// business-event bus so flows, webhooks and plugin listeners can react to access
/// grants and revocations (PLAT-270). The removal path carries only the identity,
/// since the membership no longer exists once it is published.
/// </summary>
public sealed class WorkspaceMemberBusinessEvent : IBusinessEvent
{
    private readonly IReadOnlyDictionary<string, string> _data;

    private WorkspaceMemberBusinessEvent(string eventName, string workspaceKey, IReadOnlyDictionary<string, string> data)
    {
        EventName = eventName;
        WorkspaceKey = workspaceKey;
        _data = data;
    }

    /// <inheritdoc />
    public string EventName { get; }

    /// <inheritdoc />
    public string? WorkspaceKey { get; }

    /// <summary>Builds an assigned event from an upsert result (add or role change).</summary>
    public static WorkspaceMemberBusinessEvent Assigned(WorkspaceMemberSnapshot member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return new WorkspaceMemberBusinessEvent(
            WorkspaceMemberEventTypes.Assigned,
            member.WorkspaceKey,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["workspaceKey"] = member.WorkspaceKey,
                ["userId"] = member.UserId,
                ["role"] = member.Role,
                ["email"] = member.Email ?? string.Empty,
                ["displayName"] = member.DisplayName ?? string.Empty,
            });
    }

    /// <summary>Builds a removed event from the membership identity.</summary>
    public static WorkspaceMemberBusinessEvent Removed(string workspaceKey, string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return new WorkspaceMemberBusinessEvent(
            WorkspaceMemberEventTypes.Removed,
            workspaceKey,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["workspaceKey"] = workspaceKey,
                ["userId"] = userId,
            });
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ToEventData() => _data;
}
