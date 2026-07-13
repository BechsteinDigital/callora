namespace Callora.Host.Backend.Tests.Cli;

// Nutzt das einmal pro Lauf gebaute Scaffold-Plugin aus der Collection-Fixture
// (PLAT-221) — lokal filterbar via --filter "Category!=Slow".
[Collection(ScaffoldedPluginCollection.Name)]
[Trait("Category", "Slow")]
public sealed class PluginScaffoldCliTests(ScaffoldedPluginFixture fixture)
{
    [Fact]
    public async Task PluginNew_GeneratesBuildableScaffoldWithManifestAndExtension()
    {
        Assert.True(Directory.Exists(fixture.ScaffoldDirectory));

        var pluginClassPath = Path.Combine(fixture.ScaffoldDirectory, "Application", "AcmeVoicePlugin.cs");
        Assert.True(File.Exists(fixture.CsprojPath));
        Assert.True(File.Exists(pluginClassPath));
        Assert.True(File.Exists(fixture.RegistryPath));

        var registry = await File.ReadAllTextAsync(fixture.RegistryPath).ConfigureAwait(false);
        Assert.Contains("\"pluginId\": \"acme-voice\"", registry, StringComparison.Ordinal);
        Assert.Contains("\"extensions\"", registry, StringComparison.Ordinal);
        Assert.Contains("\"workspace.navigation.main\"", registry, StringComparison.Ordinal);

        // Die Fixture wirft bei fehlgeschlagenem Build; hier bleibt der
        // Nachweis, dass das Scaffold tatsächlich kompiliert.
        Assert.True(File.Exists(fixture.AssemblyPath), fixture.BuildOutput);
    }
}
