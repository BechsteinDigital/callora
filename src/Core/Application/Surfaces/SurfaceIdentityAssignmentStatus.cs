namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Outcome of reading or changing a surface's identity provider assignment
/// (ADR-017 §5.2).
/// </summary>
public enum SurfaceIdentityAssignmentStatus
{
    /// <summary>The operation succeeded.</summary>
    Ok = 0,

    /// <summary>The workspace does not exist.</summary>
    WorkspaceNotFound,

    /// <summary>The surface does not exist in that workspace.</summary>
    SurfaceNotFound,

    /// <summary>No plugin with that id is installed.</summary>
    PluginNotFound,

    /// <summary>
    /// The plugin does not declare the <c>surface.identity</c> capability. Assigning
    /// it anyway would produce a surface whose provider can never answer.
    /// </summary>
    CapabilityMissing,
}
