namespace Callora.Host.Workspace.Api;

public sealed record WorkspaceMemberApiResponse(
    string WorkspaceKey,
    string UserId,
    string? Email,
    string? DisplayName,
    string Role,
    DateTimeOffset AssignedAtUtc);
