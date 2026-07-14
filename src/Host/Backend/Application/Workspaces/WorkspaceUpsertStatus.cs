namespace Callora.Host.Backend.Application.Workspaces;

public enum WorkspaceUpsertStatus
{
    Ok = 0,
    TenantNotFound = 1,
    InvalidPublicUrl = 2
}
