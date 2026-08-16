namespace Callora.Core.Application.Snippets;

/// <summary>
/// Die Locale-Kette eines Snippets: <c>de-DE → de → Vorgabe</c> (ADR-024 §2).
/// </summary>
/// <remarks>
/// Zwei Achsen, nicht eine: Diese Kette wird INNERHALB eines Geltungsbereichs durchlaufen, nie
/// über ihn hinweg. Andersherum schlüge die Paketdatei auf <c>de-DE</c> den Override eines
/// Betreibers auf <c>de</c> — und wer einmal „Bestellung" tippt, müsste das für de, de-DE, de-AT
/// und de-CH einzeln tun.
/// </remarks>
public static class SnippetLocaleChain
{
    /// <summary>Die Locale, bei der jede Kette endet, solange ein Workspace keine eigene führt.</summary>
    public const string DefaultLocale = "de";

    /// <summary>Von der genauesten zur allgemeinsten Locale, ohne Wiederholungen.</summary>
    public static IReadOnlyList<string> Build(string? locale, string defaultLocale = DefaultLocale)
    {
        var chain = new List<string>();

        void Add(string? candidate)
        {
            var trimmed = candidate?.Trim();
            if (!string.IsNullOrEmpty(trimmed) && !chain.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                chain.Add(trimmed);
            }
        }

        Add(locale);

        // „de-DE" trägt „de" in sich; ein Bindestrich ist die einzige Trennung, die BCP-47 an
        // dieser Stelle kennt, und mehr Bestandteile (de-DE-1996) fallen damit ebenfalls
        // schrittweise weg.
        var region = locale?.Trim();
        while (!string.IsNullOrEmpty(region) && region.Contains('-', StringComparison.Ordinal))
        {
            region = region[..region.LastIndexOf('-')];
            Add(region);
        }

        Add(defaultLocale);
        return chain;
    }
}
