namespace Callora.Host.Backend.Application.Abstractions.Workspaces;

public enum WorkspaceUpsertStatus
{
    Ok = 0,
    TenantNotFound = 1,
    InvalidPublicUrl = 2
}
