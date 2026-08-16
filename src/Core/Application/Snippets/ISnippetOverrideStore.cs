using Callora.Core.Domain.Snippets;

namespace Callora.Core.Application.Snippets;

/// <summary>
/// Speicher der Abweichungen, die ein Betreiber im Admin setzt (ADR-024 §4).
/// </summary>
/// <remarks>
/// Enthält ausschließlich Abweichungen. Beim Anlegen eines Geltungsbereichs wird nichts kopiert,
/// und ein gelöschter Eintrag führt zurück zur Basis, ohne dass jemand die Basis kennen muss.
/// </remarks>
public interface ISnippetOverrideStore
{
    /// <summary>Die Abweichungen entlang einer Geltungsbereichs-Kette für die genannten Locales.</summary>
    Task<IReadOnlyList<SnippetOverride>> ListAsync(
        IReadOnlyList<(string Scope, string ScopeKey)> scopeChain,
        IReadOnlyList<string> locales,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(SnippetOverride entry, CancellationToken cancellationToken = default);

    Task RemoveAsync(
        string snippetKey,
        string locale,
        string scope,
        string scopeKey,
        CancellationToken cancellationToken = default);
}
