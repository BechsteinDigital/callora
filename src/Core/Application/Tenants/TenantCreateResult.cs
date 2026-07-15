namespace Callora.Core.Application.Tenants;

public sealed record TenantCreateResult(
    TenantCreateStatus Status,
    TenantSnapshot? Tenant = null);
