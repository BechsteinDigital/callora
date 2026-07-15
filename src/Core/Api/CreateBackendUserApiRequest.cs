namespace Callora.Core.Api;

public sealed record CreateBackendUserApiRequest(
    string ExternalId,
    string? Email,
    string? DisplayName,
    string Password);
