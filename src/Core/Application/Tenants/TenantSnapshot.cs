namespace Callora.Core.Application.Tenants;

public sealed record TenantSnapshot(
    string TenantKey,
    string DisplayName,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
