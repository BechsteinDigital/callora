namespace Callora.Host.Backend.Api;

public sealed record UpdateBackendUserApiRequest(
    string? Email,
    string? DisplayName,
    string? Password);
