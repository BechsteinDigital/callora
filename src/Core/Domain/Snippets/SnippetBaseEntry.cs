namespace Callora.Core.Domain.Snippets;

/// <summary>
/// Ein Text, wie ein Paket ihn mitbringt — die Basis, von der ein Override abweicht (ADR-024 §4).
/// </summary>
/// <remarks>
/// Getrennt von den Abweichungen gespeichert, und das ist der Kern der Entscheidung: Ein Update
/// ersetzt die Basis eines Plugins vollständig und lässt die Abweichungen unberührt. Damit gibt es
/// keinen Fall, in dem jemand raten müsste, ob ein Wert vom Betreiber stammt oder aus der
/// Vorgängerversion.
/// </remarks>
public sealed class SnippetBaseEntry
{
    private SnippetBaseEntry()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Wem der Text gehört — das Paket, das ihn mitgebracht hat.</summary>
    public string PluginId { get; private set; } = string.Empty;

    public string SnippetKey { get; private set; } = string.Empty;

    public string Locale { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    /// <summary>Version des Pakets, aus dem dieser Stand stammt — für die Anzeige im Admin.</summary>
    public string Version { get; private set; } = string.Empty;

    public static SnippetBaseEntry Create(
        string pluginId,
        string snippetKey,
        string locale,
        string value,
        string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snippetKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        ArgumentNullException.ThrowIfNull(value);

        return new SnippetBaseEntry
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId.Trim(),
            SnippetKey = snippetKey.Trim(),
            Locale = locale.Trim(),
            Value = value,
            Version = version?.Trim() ?? string.Empty,
        };
    }
}
