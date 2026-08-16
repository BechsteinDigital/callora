using Callora.Core.Application.Snippets;
using Callora.Core.Domain.Snippets;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Callora.Core.Infrastructure.Snippets;

/// <summary>
/// Liest die in <c>registry.json</c> deklarierten Snippet-Dateien eines Pakets ein und hält damit
/// die Basis aktuell (ADR-024 §4) — das Gegenstück zu <c>RegistryConfigSchemaSyncService</c> für
/// <c>config.fields</c>.
/// </summary>
/// <remarks>
/// <code>
/// { "snippets": { "de-DE": "snippets/de-DE.json", "en-GB": "snippets/en-GB.json" } }
/// </code>
///
/// Die Pfade stehen relativ zur <c>registry.json</c> und dürfen den Ordner des Pakets nicht
/// verlassen: Ein Paket liefert seine eigenen Texte, nicht die eines beliebigen Ortes im
/// Dateisystem.
///
/// <para>
/// Jeder Schlüssel trägt die pluginId als Präfix, und das wird geprüft statt erwartet. Ohne die
/// Prüfung könnte ein Paket die Texte eines anderen überschreiben — ein Fehler, den man erst
/// bemerkt, wenn zwei Plugins zusammen laufen, und der dann nach einem Fehler im falschen Paket
/// aussieht.
/// </para>
/// </remarks>
public sealed class RegistrySnippetSyncService(
    ISnippetBaseStore store,
    ILogger<RegistrySnippetSyncService> logger)
{
    private static readonly JsonDocumentOptions JsonOptions = new() { AllowTrailingCommas = true };

    public async Task SyncFromAssemblyAsync(
        string pluginId,
        string version,
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var registryPath = FindRegistryPath(assemblyPath);
        if (registryPath is null)
        {
            return;
        }

        var pluginRoot = Path.GetDirectoryName(registryPath)!;
        var declared = ParseSnippetFiles(
            await File.ReadAllTextAsync(registryPath, cancellationToken).ConfigureAwait(false));

        var entries = new List<SnippetBaseEntry>();
        var foreignKeys = 0;

        foreach (var (locale, relativePath) in declared)
        {
            var file = ResolveInsidePlugin(pluginRoot, relativePath);
            if (file is null)
            {
                logger.LogWarning(
                    "Plugin {PluginId} declares snippets for {Locale} at {Path}, which is outside its own directory.",
                    pluginId, locale, relativePath);
                continue;
            }

            if (!File.Exists(file))
            {
                logger.LogWarning(
                    "Plugin {PluginId} declares snippets for {Locale} at {Path}, but the file is missing.",
                    pluginId, locale, relativePath);
                continue;
            }

            foreach (var (key, value) in ParseSnippetFile(
                await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false)))
            {
                if (!BelongsTo(pluginId, key))
                {
                    foreignKeys++;
                    continue;
                }

                entries.Add(SnippetBaseEntry.Create(pluginId, key, locale, value, version));
            }
        }

        if (foreignKeys > 0)
        {
            logger.LogWarning(
                "Plugin {PluginId} declares {Count} snippet keys without its own prefix; they were ignored so no plugin can overwrite another's texts.",
                pluginId,
                foreignKeys);
        }

        // Auch mit leerer Liste: Der Ersatz IST das Aufräumen — wer einen Schlüssel aus seiner
        // Datei nimmt, sähe ihn sonst weiter.
        await store.ReplaceForPluginAsync(pluginId, entries, cancellationToken).ConfigureAwait(false);
    }

    public Task ClearPluginSnippetsAsync(string pluginId, CancellationToken cancellationToken = default) =>
        store.ClearForPluginAsync(pluginId, cancellationToken);

    /// <summary>Die je Locale deklarierten Dateipfade aus einer <c>registry.json</c>.</summary>
    public static IReadOnlyDictionary<string, string> ParseSnippetFiles(string registryJson)
    {
        ArgumentNullException.ThrowIfNull(registryJson);

        using var document = JsonDocument.Parse(registryJson, JsonOptions);
        if (!document.RootElement.TryGetProperty("snippets", out var snippets) ||
            snippets.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var declared = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in snippets.EnumerateObject())
        {
            if (entry.Value.ValueKind == JsonValueKind.String &&
                entry.Value.GetString() is { Length: > 0 } path)
            {
                declared[entry.Name.Trim()] = path.Trim();
            }
        }

        return declared;
    }

    /// <summary>Die flachen Schlüssel-Wert-Paare einer Snippet-Datei.</summary>
    public static IReadOnlyDictionary<string, string> ParseSnippetFile(string snippetJson)
    {
        ArgumentNullException.ThrowIfNull(snippetJson);

        using var document = JsonDocument.Parse(snippetJson, JsonOptions);
        var snippets = new Dictionary<string, string>(StringComparer.Ordinal);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return snippets;
        }

        foreach (var entry in document.RootElement.EnumerateObject())
        {
            // Nur Zeichenketten: Verschachtelte Objekte wären eine zweite Schreibweise für
            // dasselbe, und zwei Schreibweisen sind eine Fehlerquelle mehr als eine.
            if (entry.Value.ValueKind == JsonValueKind.String)
            {
                snippets[entry.Name.Trim()] = entry.Value.GetString() ?? string.Empty;
            }
        }

        return snippets;
    }

    /// <summary>Ob ein Schlüssel zum Paket gehört — <c>pluginId.</c> als Präfix.</summary>
    public static bool BelongsTo(string pluginId, string snippetKey) =>
        snippetKey.StartsWith(pluginId + ".", StringComparison.OrdinalIgnoreCase);

    private static string? ResolveInsidePlugin(string pluginRoot, string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(pluginRoot, relativePath));
        var root = Path.GetFullPath(pluginRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.Ordinal) ? full : null;
    }

    private static string? FindRegistryPath(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return null;
        }

        // Begrenzter Aufstieg wie beim Konfigurationsschema: Die registry.json liegt in der Wurzel
        // des Pakets (bin/Debug/net10.0 sind drei Ebenen darunter), und niemals wird Richtung
        // Dateisystemwurzel gekrochen.
        var current = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(assemblyPath)) ?? string.Empty);
        for (var depth = 0; current is not null && depth < 4; depth++)
        {
            var candidate = Path.Combine(current.FullName, "registry.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
