namespace Callora.Core.Api;

public sealed record UpdateBackendUserApiRequest(
    string? Email,
    string? DisplayName,
    string? Password);
