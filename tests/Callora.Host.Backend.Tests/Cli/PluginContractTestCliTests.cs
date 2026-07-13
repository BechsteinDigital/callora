using Callora.Host.Cli.Application;

namespace Callora.Host.Backend.Tests.Cli;

// Nutzt das einmal pro Lauf gebaute Scaffold-Plugin aus der Collection-Fixture
// (PLAT-221) — lokal filterbar via --filter "Category!=Slow".
[Collection(ScaffoldedPluginCollection.Name)]
[Trait("Category", "Slow")]
public sealed class PluginContractTestCliTests(ScaffoldedPluginFixture fixture)
{
    [Fact]
    public async Task PluginTestContract_ReferenceVoipPlugin_PassesMandatoryChecks()
    {
        var repositoryRoot = fixture.RepositoryRoot;
        var voipProjectPath = Path.Combine(repositoryRoot, "custom", "plugins", "Voip", "Callora.Plugins.Voip.csproj");
        var buildResult = await ScaffoldedPluginFixture.BuildProjectAsync(voipProjectPath, repositoryRoot).ConfigureAwait(false);
        Assert.True(buildResult.Success, buildResult.Output);

        var assemblyPath = Path.Combine(
            repositoryRoot,
            "custom",
            "plugins",
            "Voip",
            "bin",
            "Debug",
            "net10.0",
            "Callora.Plugins.Voip.dll");
        var registryPath = Path.Combine(repositoryRoot, "custom", "plugins", "Voip", "registry.json");

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await CalloraCliApplication.RunAsync(
            ["plugin", "test-contract", "--assembly", assemblyPath, "--registry", registryPath],
            stdout,
            stderr,
            repositoryRoot,
            CancellationToken.None).ConfigureAwait(false);

        Assert.Equal(0, exitCode);
        Assert.Contains("All contract checks passed", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PluginTestContract_WithValidPlugin_ReturnsSuccess()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await CalloraCliApplication.RunAsync(
            ["plugin", "test-contract", "--assembly", fixture.AssemblyPath],
            stdout,
            stderr,
            fixture.RepositoryRoot,
            CancellationToken.None).ConfigureAwait(false);

        Assert.Equal(0, exitCode);
        Assert.Contains("All contract checks passed", stdout.ToString(), StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
    }

    [Fact]
    public async Task PluginTestContract_WithInvalidManifest_ReturnsActionableError()
    {
        // Nutzt die bereits gebaute Assembly der Fixture und legt nur eine
        // eigene, defekte registry.json daneben — kein zweiter Build nötig.
        var invalidRegistryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"callora-contract-invalid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(invalidRegistryDirectory);
        var registryPath = Path.Combine(invalidRegistryDirectory, "registry.json");
        var invalidRegistry = """
{
  "contractVersion": "v1",
  "schemaVersion": "1.0",
  "name": "Broken Plugin",
  "version": "0.1.0",
  "assemblyFileName": "Callora.Plugins.AcmeVoice.dll",
  "entryTypeName": "Callora.Plugins.AcmeVoice.Application.AcmeVoicePlugin"
}
""";

        try
        {
            await File.WriteAllTextAsync(registryPath, invalidRegistry, CancellationToken.None).ConfigureAwait(false);

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var exitCode = await CalloraCliApplication.RunAsync(
                ["plugin", "test-contract", "--assembly", fixture.AssemblyPath, "--registry", registryPath],
                stdout,
                stderr,
                fixture.RepositoryRoot,
                CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, exitCode);
            var errorOutput = stderr.ToString();
            Assert.Contains("MANIFEST_PLUGIN_ID_MISSING", errorOutput, StringComparison.Ordinal);
            Assert.Contains("registry.json field 'pluginId' is required", errorOutput, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(invalidRegistryDirectory, recursive: true);
        }
    }
}
