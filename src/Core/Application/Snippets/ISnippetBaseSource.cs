namespace Callora.Core.Application.Snippets;

/// <summary>
/// Die Basis eines Snippets: was Core und Plugins als Datei im Paket mitbringen (ADR-024 §4).
/// </summary>
/// <remarks>
/// Getrennt vom Speicher der Abweichungen, und das ist die Entscheidung, die das ganze System
/// trägt: Die Basis gehört dem Paket, die Abweichung dem Betreiber, beide liegen an getrennten
/// Orten. Ein Plugin-Update tauscht damit nur die Basis — es gibt keinen Fall, in dem jemand
/// raten müsste, ob ein Wert vom Betreiber stammt oder aus der Vorgängerversion.
/// </remarks>
public interface ISnippetBaseSource
{
    /// <summary>Alle Schlüssel dieser Locale, wie die Pakete sie mitliefern.</summary>
    ValueTask<IReadOnlyDictionary<string, string>> GetAsync(
        string locale,
        CancellationToken cancellationToken = default);
}
