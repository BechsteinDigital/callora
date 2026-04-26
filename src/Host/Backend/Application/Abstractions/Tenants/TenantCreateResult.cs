namespace Callora.Host.Backend.Application.Abstractions.Tenants;

public sealed record TenantCreateResult(
    TenantCreateStatus Status,
    TenantSnapshot? Tenant = null);
