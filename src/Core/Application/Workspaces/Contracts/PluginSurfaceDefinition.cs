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
        PluginSurfaceAuthentication authentication,
        string templatePluginId,
        string? templateVersion = null,
        PluginSurfaceRouting routing = PluginSurfaceRouting.Tree)
    {
        SurfaceKey = surfaceKey;
        DisplayName = displayName;
        SurfaceType = surfaceType;
        PublicPathSuffix = publicPathSuffix;
        Authentication = authentication;
        TemplatePluginId = templatePluginId;
        TemplateVersion = templateVersion;
        Routing = routing;
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
    public PluginSurfaceAuthentication Authentication { get; }

    /// <summary>Plugin whose template and workspace bundle own the surface.</summary>
    public string TemplatePluginId { get; }

    /// <summary>Optional template version recorded with the surface.</summary>
    public string? TemplateVersion { get; }

    /// <summary>
    /// Wer über die Adressen unterhalb dieser Fläche entscheidet (ADR-022).
    /// </summary>
    /// <remarks>
    /// Standard ist der Baum. Eine Anwendung, deren Adressen zur Laufzeit entstehen — ein
    /// Konferenzraum, ein Vorgang, ein Ticket —, muss es SAGEN: Ohne die Angabe antwortet jeder
    /// dieser Pfade mit 404, weil es keinen Knoten dafür gibt und keinen geben kann.
    /// </remarks>
    public PluginSurfaceRouting Routing { get; }
}
