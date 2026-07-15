namespace Callora.Administration.Api;

public sealed record CreateBackendUserApiRequest(
    string ExternalId,
    string? Email,
    string? DisplayName,
    string Password);
