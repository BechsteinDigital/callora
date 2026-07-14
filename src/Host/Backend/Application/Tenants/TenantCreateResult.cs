namespace Callora.Host.Backend.Application.Tenants;

public sealed record TenantCreateResult(
    TenantCreateStatus Status,
    TenantSnapshot? Tenant = null);
