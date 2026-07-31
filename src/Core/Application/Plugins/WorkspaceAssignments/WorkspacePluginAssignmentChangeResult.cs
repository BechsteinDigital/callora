namespace Callora.Core.Application.Plugins.WorkspaceAssignments;

public sealed class WorkspacePluginAssignmentChangeResult
{
    public WorkspacePluginAssignmentChangeResult(
        WorkspacePluginAssignmentStatus status,
        WorkspacePluginAssignment? assignment = null,
        string? message = null,
        string? errorCode = null)
    {
        Status = status;
        Assignment = assignment;
        Message = message;
        ErrorCode = errorCode;
    }

    public WorkspacePluginAssignmentStatus Status { get; }

    public WorkspacePluginAssignment? Assignment { get; }

    public string? Message { get; }

    public string? ErrorCode { get; }
}
