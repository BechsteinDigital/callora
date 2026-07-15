namespace Callora.Administration.Api;

public sealed record WorkspaceThemeAssignmentUpsertApiRequest(
    string ThemePluginId,
    string ThemeVersion,
    string? AssignedBy);
