namespace Callora.Core.Api;

public sealed record LoginApiResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    string UserId,
    string? DisplayName,
    string? Email,
    string? Role,
    string? WorkspaceKey);
