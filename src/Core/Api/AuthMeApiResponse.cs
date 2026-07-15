namespace Callora.Core.Api;

public sealed record AuthMeApiResponse(
    string UserId,
    string? DisplayName,
    string? Email,
    string? Role);
