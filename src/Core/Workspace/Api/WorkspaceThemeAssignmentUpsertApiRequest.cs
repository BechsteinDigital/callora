namespace Callora.Host.Workspace.Api;

public sealed record WorkspaceThemeAssignmentUpsertApiRequest(
    string ThemePluginId,
    string ThemeVersion,
    string? AssignedBy);
