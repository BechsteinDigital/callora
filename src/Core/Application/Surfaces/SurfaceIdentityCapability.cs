namespace Callora.Core.Application.Surfaces;

/// <summary>
/// The capability a plugin declares in its <c>registry.json</c> to become assignable
/// as a surface identity provider (ADR-017 §5.1). The admin assignment dropdown
/// filters on it instead of offering every installed plugin.
/// </summary>
public static class SurfaceIdentityCapability
{
    /// <summary>Capability key declaring "this plugin can authenticate surface visitors".</summary>
    public const string Key = "surface.identity";
}
