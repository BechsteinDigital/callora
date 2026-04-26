namespace Callora.Host.Backend.Api;

public sealed record TenantApiResponse(
    string TenantKey,
    string DisplayName,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
