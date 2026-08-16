namespace Callora.Core.Application.Snippets;

/// <summary>
/// Die Invalidierungsseite des Snippet-Caches (ADR-024 §4).
/// </summary>
/// <remarks>
/// Getrennt vom Auflösen, weil die Aufrufer verschieden sind: Auflösen tut der Renderpfad,
/// invalidieren tut, wer schreibt — die Admin-API bei einem Override, der Plugin-Lebenszyklus bei
/// einer geänderten Basis.
/// </remarks>
public interface ISnippetCache
{
    /// <summary>Nach einer Änderung an der Basis oder im globalen Bereich — sie liegen unter allem.</summary>
    void InvalidateAll();

    void InvalidateTenant(string tenantKey);

    void InvalidateWorkspace(string workspaceKey);
}
