namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>Was das Anlegen einer Seite braucht.</summary>
/// <param name="ParentSurfaceKey">
/// Unter welcher Seite sie entsteht. <b>Pflicht.</b> Eine Anwendungswurzel trägt Host,
/// Zugangsmodus und Identitätsanbieter (ADR-019 §2) — das ist Zugangsverwaltung und gehört
/// nicht in einen Editor. Ein Kind erbt all das und trägt nur Name, Segment und Elternteil;
/// genau darum darf es hier entstehen.
/// </param>
/// <param name="SurfaceKey">Technischer Schlüssel, eindeutig im Workspace.</param>
/// <param name="Label">Was im Baum steht.</param>
/// <param name="PathSegment">
/// Das eigene Segment, nicht der volle Pfad. Leer heißt: der Schlüssel wird verwendet — was
/// jemand meint, der nur einen Namen eingibt.
/// </param>
public sealed record CreatePageRequest(
    string ParentSurfaceKey,
    string SurfaceKey,
    string Label,
    string? PathSegment);
