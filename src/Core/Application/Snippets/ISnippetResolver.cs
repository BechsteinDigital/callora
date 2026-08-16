namespace Callora.Core.Application.Snippets;

/// <summary>
/// Löst die Oberflächentexte für einen Geltungsbereich und eine Locale auf (ADR-024 §2).
/// </summary>
/// <remarks>
/// Als Vertrag, damit der Cache davorgesetzt werden kann, ohne dass ein Aufrufer davon weiß —
/// dieselbe Trennung wie zwischen <c>IWorkspaceTemplateResolutionService</c> und seinem
/// gecachten Gegenstück.
/// </remarks>
public interface ISnippetResolver
{
    /// <summary>
    /// Das fertig aufgelöste Wörterbuch — je (Kette, Locale) eines, nicht je Schlüssel eines.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        string? locale,
        string? tenantKey = null,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default);
}
