namespace Callora.Administration.Api;

/// <summary>
/// Assigns a surface's identity provider. The version is not accepted from the
/// caller: it is read from the installed package, so the recorded provenance is what
/// is actually installed (ADR-017 §5.2).
/// </summary>
/// <param name="IdentityPluginId">Plugin to assign as the surface's identity provider.</param>
public sealed record SurfaceIdentityAssignmentUpsertApiRequest(string? IdentityPluginId);
