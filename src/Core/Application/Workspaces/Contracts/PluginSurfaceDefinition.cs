namespace Callora.Core.Application.Workspaces.Contracts;

/// <summary>
/// Declarative definition of a plugin-owned workspace surface. The host derives
/// the public host and workspace path prefix; plugins only supply their suffix.
/// </summary>
public sealed class PluginSurfaceDefinition
{
    /// <summary>Creates a declarative plugin-surface definition.</summary>
    public PluginSurfaceDefinition(
        string surfaceKey,
        string displayName,
        string surfaceType,
        string publicPathSuffix,
        PluginSurfaceAccessMode accessMode,
        string templatePluginId,
        string? templateVersion = null)
    {
        SurfaceKey = surfaceKey;
        DisplayName = displayName;
        SurfaceType = surfaceType;
        PublicPathSuffix = publicPathSuffix;
        AccessMode = accessMode;
        TemplatePluginId = templatePluginId;
        TemplateVersion = templateVersion;
    }

    /// <summary>Stable key unique within the workspace.</summary>
    public string SurfaceKey { get; }

    /// <summary>Operator-facing surface name.</summary>
    public string DisplayName { get; }

    /// <summary>Plugin-defined surface category.</summary>
    public string SurfaceType { get; }

    /// <summary>Path appended to the workspace's existing public prefix.</summary>
    public string PublicPathSuffix { get; }

    /// <summary>Access policy enforced by the host renderer.</summary>
    public PluginSurfaceAccessMode AccessMode { get; }

    /// <summary>Plugin whose template and workspace bundle own the surface.</summary>
    public string TemplatePluginId { get; }

    /// <summary>Optional template version recorded with the surface.</summary>
    public string? TemplateVersion { get; }
}
