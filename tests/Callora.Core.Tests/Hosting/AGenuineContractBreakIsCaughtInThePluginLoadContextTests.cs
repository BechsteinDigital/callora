using Callora.Core.Application.Plugins;
using Callora.Core.Tests.Cli;
using Xunit;

namespace Callora.Core.Tests.Hosting;

/// <summary>
/// Ein echter Vertragsbruch, hergestellt statt nachgestellt — und zwar im Ladekontext, in dem
/// Plugins tatsächlich laufen.
/// </summary>
/// <remarks>
/// <para>
/// Die Diagnose beruht darauf, dass <c>Assembly.GetTypes()</c> wirft, sobald ein Typ nicht geladen
/// werden kann. Im Standard-Ladekontext ist das gemessen. Ob es im
/// <c>PluginAssemblyLoadContext</c> genauso ist, folgt daraus <b>nicht</b>: Der löst Verträge
/// plugin-lokal auf, und wenn er dabei die passende Fassung neben der Assembly findet, entsteht der
/// Bruch gar nicht erst. Genau diese Annahme prüft dieser Test — sie ist die einzige, auf der die
/// ganze Erkennung steht.
/// </para>
/// <para>
/// Der Bruch wird deshalb gebaut und nicht als <c>ReflectionTypeLoadException</c> konstruiert: Eine
/// selbst erzeugte Ausnahme belegt, wie die Meldung formuliert wird (das prüft
/// <c>AContractBreakIsNamedAtLoadTests</c>), aber nicht, dass sie je entsteht.
/// </para>
/// <para>
/// Kostet drei SDK-Aufrufe und läuft entsprechend länger als der Rest der Suite. Das ist der Preis
/// dafür, die Annahme nicht zu glauben, sondern zu messen — und der Anlass (#283) hat mehr gekostet.
/// </para>
/// </remarks>
[Collection(PluginLoadContextCollection.Name)]
public sealed class AGenuineContractBreakIsCaughtInThePluginLoadContextTests
{
    [Fact]
    public async Task AnAssemblyBuiltAgainstAnOlderContractIsRejectedWithAUsableMessage()
    {
        var workspace = Directory.CreateTempSubdirectory("callora-contract-break-");
        try
        {
            var pluginDirectory = await BuildBrokenPluginAsync(workspace.FullName);
            var pluginPath = Path.Combine(pluginDirectory, "Fixture.BrokenPlugin.dll");
            Assert.True(File.Exists(pluginPath), $"Fixture wurde nicht gebaut: {pluginPath}");

            var registry = new SharedContractAssemblyRegistry();
            var loadContext = new PluginAssemblyLoadContext(pluginPath, registry);
            try
            {
                var assembly = loadContext.LoadFromAssemblyPath(pluginPath);

                var message = PluginContractBreakDiagnostics.Describe(assembly);

                Assert.NotNull(message);
                Assert.Contains("Fixture.BrokenPlugin", message, StringComparison.Ordinal);
                Assert.Contains("Do", message, StringComparison.Ordinal);
                Assert.Contains("neu gebaut", message, StringComparison.Ordinal);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDelete(workspace.FullName);
        }
    }

    /// <summary>
    /// Die Gegenprobe im selben Ladekontext: Passt die Vertragsfassung, kommt keine Meldung. Ohne
    /// sie bestünde der Test oben auch dann, wenn <c>Describe</c> jede Plugin-Assembly ablehnte.
    /// </summary>
    [Fact]
    public async Task TheSameAssemblyWithItsMatchingContractLoadsCleanly()
    {
        var workspace = Directory.CreateTempSubdirectory("callora-contract-ok-");
        try
        {
            var pluginDirectory = await BuildBrokenPluginAsync(workspace.FullName, breakTheContract: false);
            var pluginPath = Path.Combine(pluginDirectory, "Fixture.BrokenPlugin.dll");

            var registry = new SharedContractAssemblyRegistry();
            var loadContext = new PluginAssemblyLoadContext(pluginPath, registry);
            try
            {
                Assert.Null(PluginContractBreakDiagnostics.Describe(loadContext.LoadFromAssemblyPath(pluginPath)));
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDelete(workspace.FullName);
        }
    }

    /// <summary>
    /// Baut einen eigenen kleinen Vertrag, ein Paket dagegen — und schiebt danach die geänderte
    /// Fassung des Vertrags unter. Das ist genau die Abfolge aus dem Anlassfall, nur in Sekunden
    /// statt über zwei Tage.
    /// </summary>
    private static async Task<string> BuildBrokenPluginAsync(string workspace, bool breakTheContract = true)
    {
        var contractsDirectory = Path.Combine(workspace, "contracts");
        var pluginDirectory = Path.Combine(workspace, "plugin");
        Directory.CreateDirectory(contractsDirectory);
        Directory.CreateDirectory(pluginDirectory);

        var contractsProject = Path.Combine(contractsDirectory, "Fixture.Contracts.csproj");
        await File.WriteAllTextAsync(contractsProject, Csproj("Fixture.Contracts"));
        await File.WriteAllTextAsync(
            Path.Combine(contractsDirectory, "IThing.cs"),
            "namespace Fixture.Contracts;\npublic interface IThing { void Do(string a); }\n");

        await BuildAsync(contractsProject, workspace);
        var contractsOutput = Path.Combine(contractsDirectory, "bin", "Debug", "net10.0", "Fixture.Contracts.dll");

        var pluginProject = Path.Combine(pluginDirectory, "Fixture.BrokenPlugin.csproj");
        await File.WriteAllTextAsync(pluginProject, Csproj("Fixture.BrokenPlugin", contractsOutput));
        await File.WriteAllTextAsync(
            Path.Combine(pluginDirectory, "Thing.cs"),
            """
            using Fixture.Contracts;
            namespace Fixture.BrokenPlugin;
            public class Thing : IThing { public void Do(string a) { } }
            public class Healthy { public int Value => 42; }
            """);

        await BuildAsync(pluginProject, workspace);
        var pluginOutput = Path.Combine(pluginDirectory, "bin", "Debug", "net10.0");

        if (breakTheContract)
        {
            // Gleiche Assembly-Identität, geänderte Signatur — der Fall, den kein Versionsvergleich
            // sieht, weil sich die Version nicht bewegt hat.
            await File.WriteAllTextAsync(
                Path.Combine(contractsDirectory, "IThing.cs"),
                "namespace Fixture.Contracts;\npublic interface IThing { void Do(string a, int b); }\n");
            await BuildAsync(contractsProject, workspace);
            File.Copy(contractsOutput, Path.Combine(pluginOutput, "Fixture.Contracts.dll"), overwrite: true);
        }

        return pluginOutput;
    }

    private static async Task BuildAsync(string projectPath, string workingDirectory)
    {
        var (success, output) = await ScaffoldedPluginFixture.BuildProjectAsync(projectPath, workingDirectory);
        Assert.True(success, $"Fixture-Build fehlgeschlagen für {projectPath}:\n{output}");
    }

    private static string Csproj(string assemblyName, string? contractsReference = null)
    {
        var reference = contractsReference is null
            ? string.Empty
            : $"""
               <ItemGroup><Reference Include="Fixture.Contracts"><HintPath>{contractsReference}</HintPath></Reference></ItemGroup>
               """;

        // Ohne Central Package Management und ohne die Directory.Build.props des Repositories: Das
        // Fixture liegt außerhalb des Baums und soll nicht dessen Analyzer und Baselines erben.
        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>{assemblyName}</AssemblyName>
                <Nullable>disable</Nullable>
                <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
              </PropertyGroup>
              {reference}
            </Project>
            """;
    }

    private static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Ein noch offener Build-Worker hält gelegentlich eine Datei. Ein liegengebliebenes
            // Temp-Verzeichnis ist kein Grund, einen sonst grünen Test rot zu machen.
        }
    }
}
