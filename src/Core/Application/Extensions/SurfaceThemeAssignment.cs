namespace Callora.Core.Application.Extensions;

/// <summary>
/// Which theme a surface renders with, and where that decision came from.
/// </summary>
/// <param name="InheritedFromWorkspace">
/// True when the surface has no theme of its own and follows the workspace.
/// Removing a surface assignment returns it to this state.
/// </param>
public sealed record SurfaceThemeAssignment(
    string WorkspaceKey,
    string SurfaceKey,
    string? ThemePluginId,
    string? ThemeVersion,
    bool InheritedFromWorkspace);
