namespace Callora.Administration.Api;

public sealed record WorkspaceMemberApiResponse(
    string WorkspaceKey,
    string UserId,
    string? Email,
    string? DisplayName,
    string Role,
    DateTimeOffset AssignedAtUtc);
