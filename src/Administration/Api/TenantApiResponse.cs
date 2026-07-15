namespace Callora.Administration.Api;

public sealed record TenantApiResponse(
    string TenantKey,
    string DisplayName,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
