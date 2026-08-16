namespace Callora.Core.Application.Snippets;

/// <summary>
/// Ein Schlüssel, wie ihn die Verwaltung zeigt: was das Paket mitbringt, was hier gilt, und ob
/// jemand eingegriffen hat (ADR-024 §5).
/// </summary>
/// <param name="SnippetKey">Der Schlüssel, mit dem Template und Plugin ihn abrufen.</param>
/// <param name="Locale">Die Sprache, für die diese Zeile gilt.</param>
/// <param name="PluginId">Wer den Text mitgebracht hat; leer, wenn es nur einen Override gibt.</param>
/// <param name="BaseValue">Der Wert aus der Paketdatei; null, wenn das Paket den Schlüssel nicht (mehr) kennt.</param>
/// <param name="OverrideValue">Der Wert, den der Betreiber auf DIESER Ebene gesetzt hat.</param>
/// <remarks>
/// Basis und Abweichung stehen getrennt, weil die Verwaltung genau diese Frage beantworten muss:
/// „Was hat der Betreiber geändert?" ist eine Abfrage und kein Vergleich gegen die Paketdateien.
/// Ein Override ohne Basis ist kein Fehler — er bleibt stehen, wenn ein Paket seinen Schlüssel
/// aufgibt, und ein Downgrade stellt ihn wieder her.
/// </remarks>
public sealed record SnippetAdminEntry(
    string SnippetKey,
    string Locale,
    string PluginId,
    string? BaseValue,
    string? OverrideValue)
{
    /// <summary>Was auf dieser Ebene tatsächlich gilt.</summary>
    public string EffectiveValue => OverrideValue ?? BaseValue ?? SnippetKey;

    /// <summary>Ob hier eingegriffen wurde — nicht, ob von weiter oben etwas durchschlägt.</summary>
    public bool IsOverridden => OverrideValue is not null;

    /// <summary>Ein Override, dessen Schlüssel aus dem Paket verschwunden ist (ADR-024 §7).</summary>
    public bool IsOrphaned => OverrideValue is not null && BaseValue is null;
}
