namespace Callora.Host.Backend.Application.Abstractions.Workspaces;

public sealed record WorkspaceMemberSnapshot(
    string WorkspaceKey,
    string UserId,
    string? Email,
    string? DisplayName,
    string Role,
    DateTimeOffset AssignedAtUtc);
