namespace Callora.Core.Application.Extensions;

/// <summary>Result of assigning or clearing a surface theme.</summary>
public sealed record SurfaceThemeAssignmentResult(
    SurfaceThemeStatus Status,
    SurfaceThemeAssignment? Assignment = null,
    string? Message = null);
