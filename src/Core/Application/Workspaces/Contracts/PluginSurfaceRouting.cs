namespace Callora.Core.Application.Workspaces.Contracts;

/// <summary>
/// Wer über die Adressen unterhalb einer plugin-eigenen Fläche entscheidet (ADR-022).
/// </summary>
/// <remarks>
/// Eigener Aufzählungstyp neben <c>SurfaceRouting</c>, wie <see cref="PluginSurfaceAccessMode"/>
/// neben <c>SurfaceAccessMode</c>: Ein Plugin bindet die Vertragsschicht, nicht die Domäne. Beide
/// gleichzusetzen hieße, jede Umbenennung im Kern zur Änderung an jedem Plugin zu machen.
/// </remarks>
public enum PluginSurfaceRouting
{
    /// <summary>Der Seitenbaum ist die Wahrheit; was kein Knoten ist, antwortet mit 404.</summary>
    Tree = 0,

    /// <summary>
    /// Die Anwendung deutet ihre Unterpfade selbst — für Adressen, die zur Laufzeit entstehen und
    /// gar nicht als Knoten angelegt worden sein können.
    /// </summary>
    Application = 1,
}
