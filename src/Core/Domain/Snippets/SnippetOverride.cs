namespace Callora.Core.Domain.Snippets;

/// <summary>
/// Eine Abweichung von der Basis: der Text, den ein Betreiber für einen Schlüssel gesetzt hat
/// (ADR-024 §4).
/// </summary>
public sealed class SnippetOverride
{
    private SnippetOverride()
    {
    }

    public Guid Id { get; private set; }

    public string SnippetKey { get; private set; } = string.Empty;

    public string Locale { get; private set; } = string.Empty;

    /// <summary>global | tenant | workspace — dieselbe Kette wie in der Konfiguration.</summary>
    public string Scope { get; private set; } = string.Empty;

    /// <summary>
    /// Leer im globalen Bereich, sonst Mandanten- oder Workspace-Schlüssel.
    /// </summary>
    /// <remarks>
    /// Wird ordinal verglichen, nicht case-insensitiv: Workspace-Schlüssel werden nirgends
    /// kleingeschrieben, und ein Vergleich, der die Schreibweise ignoriert, macht aus zwei
    /// getrennten Workspaces einen.
    /// </remarks>
    public string ScopeKey { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public string UpdatedBy { get; private set; } = string.Empty;

    public static SnippetOverride Create(
        string snippetKey,
        string locale,
        string scope,
        string scopeKey,
        string value,
        string updatedBy,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snippetKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(scopeKey);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        return new SnippetOverride
        {
            Id = Guid.NewGuid(),
            SnippetKey = snippetKey.Trim(),
            Locale = locale.Trim(),
            Scope = scope.Trim(),
            ScopeKey = scopeKey.Trim(),
            Value = value,
            UpdatedBy = updatedBy.Trim(),
            UpdatedAtUtc = nowUtc,
        };
    }

    /// <summary>Setzt einen neuen Text — der Schlüssel und sein Geltungsbereich bleiben.</summary>
    public void ChangeValue(string value, string updatedBy, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        Value = value;
        UpdatedBy = updatedBy.Trim();
        UpdatedAtUtc = nowUtc;
    }
}
