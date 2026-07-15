namespace Callora.Administration.Api;

public sealed record BackendUserApiResponse(
    string ExternalId,
    string? Email,
    string? DisplayName,
    bool HasPassword,
    string? PasswordHashAlgorithm,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
