namespace Callora.Host.Backend.Api;

public sealed record CreateBackendUserApiRequest(
    string ExternalId,
    string? Email,
    string? DisplayName,
    string Password);
