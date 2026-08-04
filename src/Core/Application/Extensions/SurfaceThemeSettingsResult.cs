namespace Callora.Core.Application.Extensions;

/// <summary>Result of reading or writing the theme settings of one surface.</summary>
public sealed record SurfaceThemeSettingsResult(
    SurfaceThemeStatus Status,
    SurfaceThemeSettings? Settings = null,
    string? Message = null);
