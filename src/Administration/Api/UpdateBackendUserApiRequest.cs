namespace Callora.Administration.Api;

public sealed record UpdateBackendUserApiRequest(
    string? Email,
    string? DisplayName,
    string? Password);
