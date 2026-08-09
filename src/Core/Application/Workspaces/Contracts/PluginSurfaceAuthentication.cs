namespace Callora.Core.Application.Workspaces.Contracts;

/// <summary>
/// Which authentication a plugin asks for on the surfaces it provisions (ADR-023).
/// </summary>
public enum PluginSurfaceAuthentication
{
    /// <summary>The surface may be reached anonymously.</summary>
    Public = 0,

    /// <summary>The surface requires the identity plugin assigned to it.</summary>
    SurfaceIdentity = 1,

    /// <summary>The surface requires the host's operator sign-in.</summary>
    Administration = 2,
}
