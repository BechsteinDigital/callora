using Callora.Core.Domain.Workspaces;

namespace Callora.Core.Application.Workspaces;

/// <summary>Editable fields of a workspace surface for upsert.</summary>
/// <param name="PublicPathPrefix">
/// Das eigene Segment, nicht der volle Pfad (ADR-019 §6). Ein Kind trägt <c>partner</c>, nicht
/// <c>/portal/partner</c>: Sonst müsste beim Verschieben eines Teilbaums jeder Nachfahre
/// umgeschrieben werden, und jeder übersehene wäre eine tote URL.
/// </param>
public sealed record WorkspaceSurfaceInput(
    string SurfaceKey,
    string DisplayName,
    string SurfaceType,
    string? PublicBaseUrl,
    string? PublicHost,
    string PublicPathPrefix,
    SurfaceAccessMode AccessMode,
    string? Locale,
    string? TemplatePluginId,
    string? TemplateVersion,
    string? ThemePluginId,
    string? ThemeVersion,
    bool IsActive)
{
    /// <summary>
    /// Der Elternknoten, oder null für eine Wurzel.
    /// <para>
    /// Additiv zum Positionsparameter-Satz, damit bestehende Aufrufer unverändert eine Wurzel
    /// anlegen — was sie bisher taten und weiterhin meinen.
    /// </para>
    /// </summary>
    public string? ParentSurfaceKey { get; init; }

    /// <summary>Reihenfolge unter Geschwistern; gleiche Werte sortieren nach Schlüssel.</summary>
    public int Position { get; init; }
}
