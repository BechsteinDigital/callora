namespace Callora.Administration.Api.Admin.Snippets;

/// <summary>Ein Schlüssel, wie ihn die Verwaltung zeigt (ADR-024 §5).</summary>
public sealed record SnippetApiResponse(
    string SnippetKey,
    string Locale,
    string PluginId,
    string? BaseValue,
    string? OverrideValue,
    string EffectiveValue,
    bool IsOverridden,
    bool IsOrphaned);
