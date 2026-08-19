using System.Text.Json;
using System.Text.RegularExpressions;
using Callora.Core.Application.Plugins;
using Callora.Core.Tests.Cli;
using Callora.Host.Cli.Application;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Was <c>callora plugin new</c> erzeugt, muss in der Anleitung stehen, die der Erstnutzer
/// unmittelbar danach liest.
/// </summary>
/// <remarks>
/// <para>
/// Der Befund, aus dem dieses Gate entstand, kostet die erste Viertelstunde eines fremden
/// Entwicklers: Die Anleitung hieß ihn <c>Application/HelloPlugin.cs</c> öffnen, das Gerüst legte
/// die Datei unter <c>src/</c> ab. Dieselbe Seite nannte einen Namensraum mit <c>.Application</c>
/// am Ende und einen <c>entryTypeName</c>, der daraus folgte — beides falsch, beides das, was man
/// kopiert. Wer sich daran hält, bekommt keinen Fehler mit einem Hinweis, sondern ein Plugin, das
/// nicht lädt.
/// </para>
/// <para>
/// Der Test führt den Befehl aus, den die Anleitung abdruckt, statt die Fixture zu benutzen: Die
/// scaffoldet <c>Acme Voice</c>, die Anleitung <c>Hello</c>, und ein Vergleich zweier verschiedener
/// Namen prüft nur, dass sie verschieden sind. Erst mit demselben Namen wird aus dem Vergleich
/// eine Aussage.
/// </para>
/// <para>
/// Erwartet wird gegen die tatsächliche Ausgabe, nicht gegen eine hier zweitgeführte Konstante.
/// Ein fest verdrahteter Pfad in diesem Test wäre eine dritte Stelle mit derselben Angabe — und
/// damit dieselbe Sorte Fehler, die er verhindern soll.
/// </para>
/// </remarks>
public sealed class TheScaffoldGuideDescribesTheScaffoldTests : IAsyncLifetime
{
    private const string GuidePath = "docs-site/guides/getting-started/your-first-plugin.md";

    private string _root = string.Empty;
    private string _tempRoot = string.Empty;
    private string _scaffoldDirectory = string.Empty;
    private string _guide = string.Empty;

    public async Task InitializeAsync()
    {
        _root = ScaffoldedPluginFixture.ResolveRepositoryRoot();

        var guideFile = Path.Combine(_root, GuidePath);
        Assert.True(File.Exists(guideFile), $"Die Anleitung fehlt: {guideFile}");
        _guide = await File.ReadAllTextAsync(guideFile);

        _tempRoot = Path.Combine(Path.GetTempPath(), $"callora-guide-scaffold-{Guid.NewGuid():N}");
        _scaffoldDirectory = Path.Combine(_tempRoot, "Hello");

        // Name und Id aus der Anleitung selbst, nicht aus einer Konstante hier: Benennt die Seite
        // ihr Beispiel um, prüft der Test danach das umbenannte Beispiel.
        var (name, id) = ScaffoldArgumentsFromGuide();

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await CalloraCliApplication.RunAsync(
            ["plugin", "new", id, "--name", name, "--id", id, "--output", _scaffoldDirectory],
            stdout,
            stderr,
            _root,
            CancellationToken.None);

        Assert.True(exitCode == 0, $"Das Gerüst der Anleitung ließ sich nicht erzeugen: {stderr}{stdout}");
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public void TheGuideNamesTheFileTheScaffoldActuallyWrites()
    {
        var entryFile = Path.GetFileName(
            Directory.EnumerateFiles(Path.Combine(_scaffoldDirectory, "src"), "*.cs").Single());

        Assert.Contains($"src/{entryFile}", _guide, StringComparison.Ordinal);

        // Der frühere Pfad, wörtlich. Dass er verschwunden ist, ist eine eigene Zusicherung —
        // er stand lange genug da, um in fremden Anleitungen weiterzuleben.
        Assert.DoesNotContain($"Application/{entryFile}", _guide, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGuideQuotesTheNamespaceTheScaffoldWrites()
    {
        var entry = Directory.EnumerateFiles(Path.Combine(_scaffoldDirectory, "src"), "*.cs").Single();
        var declared = Regex
            .Match(File.ReadAllText(entry), @"^namespace\s+([^;\s]+)\s*;", RegexOptions.Multiline)
            .Groups[1].Value;

        Assert.False(string.IsNullOrWhiteSpace(declared), $"{entry} deklariert keinen Namensraum.");
        Assert.Contains($"namespace {declared};", _guide, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGuideQuotesTheEntryTypeTheScaffoldDeclares()
    {
        var declared = Manifest().GetProperty("entryTypeName").GetString();

        Assert.False(string.IsNullOrWhiteSpace(declared));
        Assert.Contains(declared!, _guide, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGuideQuotesTheContractVersionTheScaffoldDeclares()
    {
        var declared = Manifest().GetProperty("contractVersion").GetString();

        Assert.False(string.IsNullOrWhiteSpace(declared));
        Assert.Contains($"\"contractVersion\": \"{declared}\"", _guide, StringComparison.Ordinal);
    }

    /// <summary>
    /// Das Gerüst darf nicht auf einer Fassung stehen, von der die Plattform wegführt.
    /// </summary>
    /// <remarks>
    /// Bis 08/2026 schrieb es <c>v1</c>, während <see cref="PluginContractVersionPolicy"/> längst
    /// <c>v2</c> als unterstützt und <c>v1</c> als veraltet führte. Der Weg, den jeder Erstnutzer
    /// geht, endete damit auf der veralteten Stufe — samt Verfallswarnung beim Installieren, die
    /// aus dem Werkzeug stammte und nicht aus etwas, das der Entwickler getan hat.
    /// </remarks>
    [Fact]
    public void TheScaffoldDeclaresASupportedContractVersion()
    {
        var declared = Manifest().GetProperty("contractVersion").GetString();

        Assert.True(
            PluginContractVersionPolicy.TryGet(declared!, out var support),
            $"Das Gerüst schreibt contractVersion '{declared}', die PluginContractVersionPolicy nicht kennt.");

        Assert.True(
            support.Status == PluginContractSupportStatus.Supported,
            $"Das Gerüst schreibt contractVersion '{declared}' ({support.Status}). "
            + "Ein neu erzeugtes Plugin gehört auf die unterstützte Fassung.");
    }

    private JsonElement Manifest() =>
        JsonDocument
            .Parse(File.ReadAllText(Path.Combine(_scaffoldDirectory, "registry.json")))
            .RootElement;

    private (string Name, string Id) ScaffoldArgumentsFromGuide()
    {
        var name = Regex.Match(_guide, @"--name\s+""([^""]+)""").Groups[1].Value;
        var id = Regex.Match(_guide, @"--id\s+(\S+)").Groups[1].Value;

        Assert.False(
            string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id),
            "Die Anleitung zeigt keinen 'plugin new'-Aufruf mit --name und --id mehr. "
            + "Wird der Befehl umgeschrieben, gehört dieser Test mitgezogen — er prüft genau das, "
            + "was dort steht.");

        return (name, id);
    }
}
