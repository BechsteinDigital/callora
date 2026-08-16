namespace Callora.Core.Application.Snippets;

/// <summary>
/// Die aufgelösten Texte der laufenden Anfrage (ADR-024 §2, §5).
/// </summary>
/// <remarks>
/// Existiert wegen eines Bruchs zwischen zwei Welten: <see cref="Microsoft.Extensions.Localization.IStringLocalizer"/>
/// ist synchron, das Auflösen geht über Datenbank und Cache. Ein blockierender Aufruf im
/// Anfragepfad wäre die naheliegende und die falsche Antwort — er kostet einen Thread-Pool-Thread
/// je Anfrage, und zwar genau dann, wenn viele gleichzeitig kommen.
///
/// <para>
/// Deshalb wird einmal je Anfrage asynchron geladen, und der Localizer liest danach nur noch.
/// Wurde nichts geladen, ist der Katalog leer und jeder Schlüssel fällt auf seinen Vorgabewert
/// zurück — eine Oberfläche ohne Snippets zeigt dann ihren eingebauten Text und keine Schlüssel.
/// </para>
/// </remarks>
public interface ISnippetCatalog
{
    /// <summary>Was für diese Anfrage gilt; leer, solange nichts geladen wurde.</summary>
    IReadOnlyDictionary<string, string> Snippets { get; }

    /// <summary>Die Locale, für die geladen wurde — leer, solange nichts geladen wurde.</summary>
    string Locale { get; }

    Task LoadAsync(
        string? locale,
        string? tenantKey = null,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default);
}
