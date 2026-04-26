namespace Callora.Host.Backend.Application.Abstractions.Tenants;

public enum TenantDeleteStatus
{
    Deleted = 0,
    NotFound = 1,
    HasWorkspaces = 2
}
