namespace Callora.Host.Backend.Application.Abstractions.Tenants;

public sealed record TenantSnapshot(
    string TenantKey,
    string DisplayName,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
