namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>Wohin eine Seite verschoben wird.</summary>
/// <param name="ParentSurfaceKey">
/// Das neue Übergeordnete. <b>Pflicht</b> — eine Seite zur Anwendungswurzel zu machen hieße,
/// ihr Host, Zugangsmodus und Identitätsanbieter zu geben, und das ist Zugangsverwaltung.
/// </param>
/// <param name="Position">Reihenfolge unter den neuen Geschwistern.</param>
public sealed record MovePageRequest(string ParentSurfaceKey, int Position);
