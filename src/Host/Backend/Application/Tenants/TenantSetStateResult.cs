namespace Callora.Host.Backend.Application.Tenants;

public sealed record TenantSetStateResult(
    TenantSetStateStatus Status,
    TenantSnapshot? Tenant = null);
