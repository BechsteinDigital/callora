namespace Callora.Core.Application.Workspaces.Contracts;

/// <summary>Published plugin-facing access modes for a provisioned surface.</summary>
public enum PluginSurfaceAccessMode
{
    /// <summary>The surface may be reached anonymously.</summary>
    Public = 0,

    /// <summary>The surface requires an authenticated workspace identity.</summary>
    Authenticated = 1,

    /// <summary>The surface supports both anonymous invitation and authenticated routes.</summary>
    Mixed = 2,
}
