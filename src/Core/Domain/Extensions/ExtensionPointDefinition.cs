using Callora.Core.Extensibility;

namespace Callora.Core.Domain.Extensions;

/// <summary>
/// Defines one extension point and the surface where it is available.
/// </summary>
public sealed record ExtensionPointDefinition(
    [ExtensionPointId] string ExtensionPointId,
    ExtensionSurface Surface,
    string RequiredScope);
