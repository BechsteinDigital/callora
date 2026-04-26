namespace Callora.Host.Backend.Application.Abstractions.Tenants;

public sealed record TenantSetStateResult(
    TenantSetStateStatus Status,
    TenantSnapshot? Tenant = null);
