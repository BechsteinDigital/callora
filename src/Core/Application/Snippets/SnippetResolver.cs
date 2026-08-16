using Callora.Core.Application.Configuration;
using Callora.Core.Domain.Snippets;

namespace Callora.Core.Application.Snippets;

/// <summary>
/// Löst die Oberflächentexte für einen Geltungsbereich und eine Locale auf (ADR-024 §2).
/// </summary>
/// <remarks>
/// Zwei Ketten, und ihre Reihenfolge ist die Entscheidung:
///
/// <code>
/// Geltungsbereich:  workspace → tenant → global → Paketdatei (Basis)
/// Locale:           de-DE → de → Vorgabe
/// </code>
///
/// <b>Der Geltungsbereich wird zuerst durchlaufen, die Locale erst innerhalb.</b> Andersherum
/// schlüge die Paketdatei auf <c>de-DE</c> den Override eines Betreibers auf <c>de</c>, weil sie
/// spezifischer wäre — und wer einmal „Bestellung" tippt, müsste das für de, de-DE, de-AT und
/// de-CH einzeln tun. Der Satz, der die Reihenfolge trägt: Ein Override ist eine Absicht, eine
/// Regionalvariante nur eine Verfeinerung.
///
/// <para>
/// Aufgelöst wird je (Kette, Locale) ein ganzes Wörterbuch, nicht je Schlüssel: Der Renderpfad
/// zieht damit einen Eintrag statt N Abfragen. Der Cache setzt in derselben Granularität an.
/// </para>
/// </remarks>
public sealed class SnippetResolver(ISnippetBaseSource baseSource, ISnippetOverrideStore overrides)
{
    public async Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        string? locale,
        string? tenantKey = null,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default)
    {
        var locales = SnippetLocaleChain.Build(locale);
        var scopeChain = SystemConfigResolver.BuildScopeChain(tenantKey, workspaceKey);

        // Von der allgemeinsten Ebene zur spezifischsten schreiben, damit die spezifischere
        // gewinnt — dasselbe Vorgehen wie beim Auflösen der Konfiguration.
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var candidate in locales.AsEnumerable().Reverse())
        {
            var basis = await baseSource.GetAsync(candidate, cancellationToken).ConfigureAwait(false);
            foreach (var (key, value) in basis)
            {
                resolved[key] = value;
            }
        }

        var entries = await overrides.ListAsync(scopeChain, locales, cancellationToken).ConfigureAwait(false);

        foreach (var (scope, scopeKey) in scopeChain)
        {
            foreach (var candidate in locales.AsEnumerable().Reverse())
            {
                foreach (var entry in Matching(entries, scope, scopeKey, candidate))
                {
                    resolved[entry.SnippetKey] = entry.Value;
                }
            }
        }

        return resolved;
    }

    private static IEnumerable<SnippetOverride> Matching(
        IReadOnlyList<SnippetOverride> entries,
        string scope,
        string scopeKey,
        string locale)
        => entries.Where(entry =>
            entry.Scope == scope
            // Ordinal wie im Store und wie im SystemConfigResolver.
            && string.Equals(entry.ScopeKey, scopeKey, StringComparison.Ordinal)
            && string.Equals(entry.Locale, locale, StringComparison.OrdinalIgnoreCase));
}
