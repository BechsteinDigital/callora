namespace Callora.Host.Backend.Application.Abstractions.Workspaces;

public enum WorkspaceMemberUpsertStatus
{
    Ok = 0,
    WorkspaceNotFound = 1,
    UserNotFound = 2
}
