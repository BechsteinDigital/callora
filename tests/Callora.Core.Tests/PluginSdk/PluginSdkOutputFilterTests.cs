using Callora.Core.Tests.Cli;
using System.Diagnostics;

namespace Callora.Core.Tests.PluginSdk;

/// <summary>
/// Führt die beiden Filter-Targets aus Callora.Plugin.Sdk gegen handgesetzte Item-Listen
/// aus und prüft, was sie stehen lassen.
///
/// Warum nicht am echten Plugin-Build: Der einzige Plugin-Build im Repository ist das
/// Scaffold aus PluginScaffoldCliTests, und das bekommt im Repository ProjectReferences
/// statt des SDK-PAKETS (PluginScaffolder.BuildPluginContractsReference) — die .targets
/// werden dort also gar nicht importiert. Genau deshalb konnte die Regel brechen, ohne
/// dass ein Test rot wurde.
///
/// Die Metadaten unten sind nicht erfunden, sondern an einem echten Plugin-Publish
/// abgelesen (dotnet publish -getItem:ResolvedFileToPublish): Paket-Assets tragen
/// NuGetPackageId, die eigene Ausgabe und ProjectReference-Ausgaben tragen es nicht.
/// </summary>
[Trait("Category", "Slow")]
public sealed class PluginSdkOutputFilterTests
{
    [Fact]
    public async Task TheFilterRemovesPlatformPackagesAndKeepsWhatTheProjectItselfBuilt()
    {
        var surviving = await RunFilterAsync();

        // Der Zweck des Pakets: Plattform-Assemblies aus einem NuGet-Paket bleiben draußen,
        // damit Host und Plugin nicht denselben Vertragstyp zweimal laden.
        Assert.DoesNotContain("publish:Callora.Core.dll", surviving);
        Assert.DoesNotContain("publish:Callora.dll", surviving);
        Assert.DoesNotContain("build:Callora.Workspace.dll", surviving);

        // Der Regressionsfall: Ein Plugin, das selbst "Callora." heißt, hat sich mit der
        // reinen Namensregel aus seinem eigenen Ausgabeordner gefiltert. `dotnet publish`
        // lieferte ein Verzeichnis aus, in dem die deps.json eine Haupt-DLL beschrieb, die
        // nicht danebenlag — und der Signierschritt fand die deklarierte Assembly nicht.
        Assert.Contains("publish:Callora.Plugin.AcmeVoice.dll", surviving);
        Assert.Contains("publish:Callora.Plugin.AcmeVoice.runtimeconfig.json", surviving);

        // Zweiter Regressionsfall, eine Ebene tiefer: die plugin-eigene Vertrags-Assembly
        // aus einer ProjectReference. Kein Host stellt sie, also muss sie mitreisen — beim
        // Publish wie beim Build.
        Assert.Contains("publish:Callora.Plugin.AcmeVoice.Abstractions.dll", surviving);
        Assert.Contains("build:Callora.Plugin.AcmeVoice.Abstractions.dll", surviving);

        // CalloraVoipSdk: kein Punkt hinter "Callora", also plugin-lokal — beim Bauen wie
        // zur Laufzeit (PluginAssemblyLoadContext.Load).
        Assert.Contains("publish:CalloraVoipSdk.dll", surviving);
        Assert.Contains("publish:Npgsql.dll", surviving);
    }

    [Fact]
    public async Task CalloraKeepInOutput_ExemptsANamedAssemblyFromTheFilter()
    {
        var surviving = await RunFilterAsync(keepInOutput: "Callora.Core");

        Assert.Contains("publish:Callora.Core.dll", surviving);
        // Das Ventil nennt genau eine Assembly und öffnet nicht die Regel.
        Assert.DoesNotContain("publish:Callora.dll", surviving);
    }

    /// <summary>
    /// Baut ein MSBuild-Projekt, das die echten .props/.targets importiert, hängt die
    /// beiden Listen so an, wie ein Plugin-Build sie stellt, und gibt zurück, was die
    /// Targets übrig lassen.
    /// </summary>
    private static async Task<IReadOnlyList<string>> RunFilterAsync(string? keepInOutput = null)
    {
        var repositoryRoot = ScaffoldedPluginFixture.ResolveRepositoryRoot();
        var buildDirectory = Path.Combine(repositoryRoot, "src", "Plugin.Sdk", "build");
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"callora-sdk-filter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);

        try
        {
            var projectPath = Path.Combine(temporaryRoot, "filter.proj");
            var resultPath = Path.Combine(temporaryRoot, "surviving.txt");
            await File.WriteAllTextAsync(projectPath, BuildFixtureProject(buildDirectory));

            var arguments =
                $"msbuild \"{projectPath}\" -t:WriteSurvivors -p:ResultFile=\"{resultPath}\" " +
                "-nologo -verbosity:quiet -nodeReuse:false";
            if (keepInOutput is not null)
            {
                arguments += $" -p:CalloraKeepInOutput={keepInOutput}";
            }

            var (exitCode, output) = await RunDotnetAsync(arguments, temporaryRoot);
            Assert.True(exitCode == 0, $"MSBuild-Lauf fehlgeschlagen: {output}");
            Assert.True(File.Exists(resultPath), $"Kein Ergebnis geschrieben: {output}");

            return await File.ReadAllLinesAsync(resultPath);
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static string BuildFixtureProject(string buildDirectory) =>
        $"""
         <Project>

           <Import Project="{Path.Combine(buildDirectory, "Callora.Plugin.Sdk.props")}" />
           <Import Project="{Path.Combine(buildDirectory, "Callora.Plugin.Sdk.targets")}" />

           <ItemGroup>
             <!-- Plattform aus einem Paket: der Fall, für den das Paket existiert. -->
             <ResolvedFileToPublish Include="pkg/Callora.Core.dll" NuGetPackageId="Callora.Core" />
             <ResolvedFileToPublish Include="pkg/Callora.dll" NuGetPackageId="Callora" />
             <ReferenceCopyLocalPaths Include="pkg/Callora.Workspace.dll" NuGetPackageId="Callora.Workspace" />

             <!-- Was dieses Projekt selbst baut: keine NuGetPackageId. -->
             <ResolvedFileToPublish Include="obj/Callora.Plugin.AcmeVoice.dll" />
             <ResolvedFileToPublish Include="obj/Callora.Plugin.AcmeVoice.runtimeconfig.json" />

             <!-- Ausgabe einer eigenen ProjectReference: ebenfalls ohne NuGetPackageId,
                  zusätzlich mit ReferenceSourceTarget wie im echten Build. -->
             <ResolvedFileToPublish Include="obj/Callora.Plugin.AcmeVoice.Abstractions.dll"
                                    ReferenceSourceTarget="ProjectReference" />
             <ReferenceCopyLocalPaths Include="obj/Callora.Plugin.AcmeVoice.Abstractions.dll"
                                      ReferenceSourceTarget="ProjectReference" />

             <!-- Echte plugin-private Paketabhängigkeiten. -->
             <ResolvedFileToPublish Include="pkg/CalloraVoipSdk.dll" NuGetPackageId="CalloraVoipSdk" />
             <ResolvedFileToPublish Include="pkg/Npgsql.dll" NuGetPackageId="Npgsql" />
           </ItemGroup>

           <!-- Die Hooks, an denen die Targets hängen. Im echten Build füllt das .NET SDK
                sie; hier stehen sie leer, weil die Item-Listen schon gesetzt sind. -->
           <Target Name="ResolveReferences" />
           <Target Name="ComputeResolvedFilesToPublishList" />

           <Target Name="WriteSurvivors"
                   DependsOnTargets="ResolveReferences;ComputeResolvedFilesToPublishList">
             <WriteLinesToFile File="$(ResultFile)"
                               Lines="@(ResolvedFileToPublish->'publish:%(Filename)%(Extension)');@(ReferenceCopyLocalPaths->'build:%(Filename)%(Extension)')"
                               Overwrite="true" />
           </Target>

         </Project>

         """;

    private static async Task<(int ExitCode, string Output)> RunDotnetAsync(string arguments, string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        // Dieselbe Prozess-Hygiene wie in ScaffoldedPluginFixture: ohne sie bleiben
        // MSBuild-Worker mit den geerbten Pipe-Handles zurück, und ReadToEndAsync hängt
        // bis zu deren Idle-Timeout (PLAT-221).
        process.StartInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        process.StartInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, string.Concat(await outputTask, Environment.NewLine, await errorTask));
    }
}
