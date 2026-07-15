namespace Callora.Core.Application.Workspaces;

public enum WorkspaceUpsertStatus
{
    Ok = 0,
    TenantNotFound = 1,
    InvalidPublicUrl = 2
}
