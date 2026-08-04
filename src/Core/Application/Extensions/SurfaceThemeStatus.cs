namespace Callora.Core.Application.Extensions;

/// <summary>Outcome of a surface theme operation.</summary>
public enum SurfaceThemeStatus
{
    Ok = 0,

    /// <summary>The workspace does not exist (or is outside the host's tenant).</summary>
    WorkspaceNotFound = 1,

    /// <summary>The workspace exists, but has no surface with that key.</summary>
    SurfaceNotFound = 2,

    /// <summary>No active theme definition matches the requested plugin and version.</summary>
    ThemeNotFound = 3,

    /// <summary>Settings were addressed while neither surface nor workspace has a theme.</summary>
    NoThemeAssigned = 4,
}
