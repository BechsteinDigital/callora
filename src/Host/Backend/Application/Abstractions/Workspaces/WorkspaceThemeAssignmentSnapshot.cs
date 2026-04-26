namespace Callora.Host.Backend.Application.Abstractions.Workspaces;

public sealed record WorkspaceThemeAssignmentSnapshot(
    string WorkspaceKey,
    string? ThemePluginId,
    string? ThemeVersion,
    string? AssignedBy,
    DateTimeOffset? AssignedAtUtc);
