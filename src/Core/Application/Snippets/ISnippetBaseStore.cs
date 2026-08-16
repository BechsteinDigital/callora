using Callora.Core.Domain.Snippets;

namespace Callora.Core.Application.Snippets;

/// <summary>
/// Schreibzugriff auf die Basis: was Core und Plugins als Datei mitbringen (ADR-024 §4).
/// </summary>
public interface ISnippetBaseStore
{
    /// <summary>
    /// Ersetzt die Basis eines Pakets vollständig.
    /// </summary>
    /// <remarks>
    /// Vollständig und nicht additiv, aus demselben Grund wie beim Konfigurationsschema: Wer einen
    /// Schlüssel aus seiner Datei nimmt, sähe ihn sonst weiter — auf dem Update-Pfad löscht sonst
    /// nichts. Eine leere Liste IST das Aufräumen.
    /// </remarks>
    Task ReplaceForPluginAsync(
        string pluginId,
        IReadOnlyList<SnippetBaseEntry> entries,
        CancellationToken cancellationToken = default);

    /// <summary>Entfernt die Basis eines Pakets — die Abweichungen des Betreibers bleiben stehen.</summary>
    Task ClearForPluginAsync(string pluginId, CancellationToken cancellationToken = default);
}
