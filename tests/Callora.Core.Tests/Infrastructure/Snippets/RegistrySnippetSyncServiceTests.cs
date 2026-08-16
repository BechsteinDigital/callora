using Callora.Core.Application.Snippets;
using Callora.Core.Domain.Snippets;
using Callora.Core.Infrastructure.Snippets;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Core.Tests.Infrastructure.Snippets;

/// <summary>
/// Die Basis kommt als Datei im Paket und wird beim Installieren und Aktualisieren eingelesen
/// (ADR-024 §4) — dieselbe Mechanik wie beim Konfigurationsschema.
/// </summary>
public sealed class RegistrySnippetSyncServiceTests : IDisposable
{
    private readonly string _pluginRoot = Path.Combine(
        Path.GetTempPath(),
        $"callora-snippets-{Guid.NewGuid():N}");

    [Fact]
    public async Task SyncFromAssembly_ReadsEveryDeclaredLocale()
    {
        WriteRegistry("""{ "snippets": { "de-DE": "snippets/de-DE.json", "en-GB": "snippets/en-GB.json" } }""");
        WriteSnippets("de-DE", """{ "composer.editor.save": "Speichern" }""");
        WriteSnippets("en-GB", """{ "composer.editor.save": "Save" }""");
        var store = new RecordingSnippetBaseStore();

        await Sync(store).SyncFromAssemblyAsync("composer", "1.2.0", AssemblyPath());

        Assert.Equal(
            [("composer.editor.save", "de-DE", "Speichern"), ("composer.editor.save", "en-GB", "Save")],
            store.Replaced.Select(entry => (entry.SnippetKey, entry.Locale, entry.Value)).Order().ToArray());
        Assert.All(store.Replaced, entry => Assert.Equal("1.2.0", entry.Version));
    }

    // Ohne diese Prüfung könnte ein Paket die Texte eines anderen überschreiben — ein Fehler, den
    // man erst bemerkt, wenn zwei Plugins zusammen laufen, und der dann nach einem Fehler im
    // falschen Paket aussieht.
    [Fact]
    public async Task SyncFromAssembly_IgnoresKeysThatBelongToAnotherPlugin()
    {
        WriteRegistry("""{ "snippets": { "de-DE": "snippets/de-DE.json" } }""");
        WriteSnippets("de-DE", """
            { "composer.editor.save": "Speichern", "communication.call.answer": "Annehmen" }
            """);
        var store = new RecordingSnippetBaseStore();

        await Sync(store).SyncFromAssemblyAsync("composer", "1.0.0", AssemblyPath());

        Assert.Equal(["composer.editor.save"], store.Replaced.Select(entry => entry.SnippetKey).ToArray());
    }

    // Ein Paket liefert seine eigenen Texte, nicht die eines beliebigen Ortes im Dateisystem.
    [Fact]
    public async Task SyncFromAssembly_RefusesAPathOutsideThePluginDirectory()
    {
        WriteRegistry("""{ "snippets": { "de-DE": "../../../etc/passwd" } }""");
        var store = new RecordingSnippetBaseStore();

        await Sync(store).SyncFromAssemblyAsync("composer", "1.0.0", AssemblyPath());

        Assert.Empty(store.Replaced);
        Assert.True(store.WasReplaced);
    }

    // Wer einen Schlüssel aus seiner Datei nimmt, sähe ihn sonst weiter: Der Ersatz mit leerer
    // Liste IST das Aufräumen — dieselbe Begründung wie beim Konfigurationsschema.
    [Fact]
    public async Task SyncFromAssembly_WithoutAnyDeclaration_StillReplacesSoRemovedKeysDisappear()
    {
        WriteRegistry("""{ "pluginId": "composer" }""");
        var store = new RecordingSnippetBaseStore();

        await Sync(store).SyncFromAssemblyAsync("composer", "1.0.0", AssemblyPath());

        Assert.True(store.WasReplaced);
        Assert.Empty(store.Replaced);
    }

    [Fact]
    public async Task SyncFromAssembly_WithoutARegistry_DoesNothingAtAll()
    {
        Directory.CreateDirectory(_pluginRoot);
        var store = new RecordingSnippetBaseStore();

        await Sync(store).SyncFromAssemblyAsync("composer", "1.0.0", AssemblyPath());

        Assert.False(store.WasReplaced);
    }

    [Fact]
    public void ParseSnippetFile_TakesStringsAndLeavesEverythingElse()
    {
        var snippets = RegistrySnippetSyncService.ParseSnippetFile("""
            { "a.text": "Text", "a.nested": { "deep": "Wert" }, "a.number": 42 }
            """);

        Assert.Equal(new Dictionary<string, string> { ["a.text"] = "Text" }, snippets);
    }

    private RegistrySnippetSyncService Sync(ISnippetBaseStore store)
        => new(store, NullLogger<RegistrySnippetSyncService>.Instance);

    private string AssemblyPath() => Path.Combine(_pluginRoot, "bin", "Debug", "net10.0", "Plugin.dll");

    private void WriteRegistry(string json)
    {
        Directory.CreateDirectory(_pluginRoot);
        File.WriteAllText(Path.Combine(_pluginRoot, "registry.json"), json);
    }

    private void WriteSnippets(string locale, string json)
    {
        var directory = Path.Combine(_pluginRoot, "snippets");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{locale}.json"), json);
    }

    public void Dispose()
    {
        if (Directory.Exists(_pluginRoot))
        {
            Directory.Delete(_pluginRoot, recursive: true);
        }
    }

    private sealed class RecordingSnippetBaseStore : ISnippetBaseStore
    {
        public bool WasReplaced { get; private set; }

        public IReadOnlyList<SnippetBaseEntry> Replaced { get; private set; } = [];

        public Task ReplaceForPluginAsync(
            string pluginId,
            IReadOnlyList<SnippetBaseEntry> entries,
            CancellationToken cancellationToken = default)
        {
            WasReplaced = true;
            Replaced = entries;
            return Task.CompletedTask;
        }

        public Task ClearForPluginAsync(string pluginId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
