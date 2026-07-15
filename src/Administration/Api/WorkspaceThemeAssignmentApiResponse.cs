namespace Callora.Administration.Api;

public sealed record WorkspaceThemeAssignmentApiResponse(
    string WorkspaceKey,
    string? ThemePluginId,
    string? ThemeVersion,
    string? AssignedBy,
    DateTimeOffset? AssignedAtUtc);
