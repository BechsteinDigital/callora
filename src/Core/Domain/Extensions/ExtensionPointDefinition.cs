namespace Callora.Core.Domain.Extensions;

/// <summary>
/// Defines one extension point and the surface where it is available.
/// </summary>
public sealed record ExtensionPointDefinition(
    string ExtensionPointId,
    ExtensionSurface Surface,
    string RequiredScope);
