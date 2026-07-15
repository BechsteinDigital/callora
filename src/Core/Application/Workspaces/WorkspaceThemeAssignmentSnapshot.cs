namespace Callora.Core.Application.Workspaces;

public sealed record WorkspaceThemeAssignmentSnapshot(
    string WorkspaceKey,
    string? ThemePluginId,
    string? ThemeVersion,
    string? AssignedBy,
    DateTimeOffset? AssignedAtUtc);
