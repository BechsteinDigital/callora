namespace Callora.Core.Application.Tenants;

public sealed record TenantSetStateResult(
    TenantSetStateStatus Status,
    TenantSnapshot? Tenant = null);
