namespace Callora.Core.Application.Plugins.WorkspaceAssignments;

public sealed class WorkspacePluginAssignmentListResult
{
    public WorkspacePluginAssignmentListResult(
        WorkspacePluginAssignmentStatus status,
        IReadOnlyList<WorkspacePluginAssignment> items,
        string? message = null)
    {
        Status = status;
        Items = items;
        Message = message;
    }

    public WorkspacePluginAssignmentStatus Status { get; }

    public IReadOnlyList<WorkspacePluginAssignment> Items { get; }

    public string? Message { get; }
}
