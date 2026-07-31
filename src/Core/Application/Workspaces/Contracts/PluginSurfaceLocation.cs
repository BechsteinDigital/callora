namespace Callora.Core.Application.Workspaces.Contracts;

/// <summary>The resolved route of an ensured plugin-owned surface.</summary>
public sealed class PluginSurfaceLocation
{
    /// <summary>Creates the resolved public location of a plugin surface.</summary>
    public PluginSurfaceLocation(
        string workspaceKey,
        string surfaceKey,
        string publicPath,
        string publicUrl)
    {
        WorkspaceKey = workspaceKey;
        SurfaceKey = surfaceKey;
        PublicPath = publicPath;
        PublicUrl = publicUrl;
    }

    /// <summary>Workspace that owns the surface.</summary>
    public string WorkspaceKey { get; }

    /// <summary>Stable surface key.</summary>
    public string SurfaceKey { get; }

    /// <summary>Same-origin path clients may navigate to.</summary>
    public string PublicPath { get; }

    /// <summary>Absolute URL when a public workspace host is configured, otherwise the path.</summary>
    public string PublicUrl { get; }
}
