namespace Callora.Host.Workspace.Api;

public sealed record WorkspaceThemeAssignmentApiResponse(
    string WorkspaceKey,
    string? ThemePluginId,
    string? ThemeVersion,
    string? AssignedBy,
    DateTimeOffset? AssignedAtUtc);
