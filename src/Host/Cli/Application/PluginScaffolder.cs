using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Callora.Host.Cli.Application;

internal sealed class PluginScaffolder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<PluginScaffoldResult> ScaffoldAsync(
        PluginScaffoldRequest request,
        string currentDirectory,
        CancellationToken cancellationToken)
    {
        if (!PluginScaffoldNaming.IsValidPluginId(request.PluginId))
        {
            return PluginScaffoldResult.Fail("Invalid plugin id. Allowed: a-z, A-Z, 0-9, '.', '-', '_'.");
        }

        var outputDirectory = Path.GetFullPath(request.OutputDirectory);
        if (!request.Force && Directory.Exists(outputDirectory) && Directory.EnumerateFileSystemEntries(outputDirectory).Any())
        {
            return PluginScaffoldResult.Fail($"Output directory is not empty: {outputDirectory}");
        }

        var projectName = $"Callora.Plugins.{PluginScaffoldNaming.ToPascalCase(request.Name)}";
        var pluginClassName = $"{PluginScaffoldNaming.ToPascalCase(request.Name)}Plugin";
        var namespaceName = projectName;

        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(Path.Combine(outputDirectory, "src"));

        var pluginContractsReference = BuildPluginContractsReference(outputDirectory, currentDirectory);

        var csprojPath = Path.Combine(outputDirectory, $"{projectName}.csproj");
        var pluginClassPath = Path.Combine(outputDirectory, "src", $"{pluginClassName}.cs");
        var registryPath = Path.Combine(outputDirectory, "registry.json");

        await File.WriteAllTextAsync(csprojPath, BuildProjectFile(pluginContractsReference), Encoding.UTF8, cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(
                pluginClassPath,
                BuildPluginClass(namespaceName, pluginClassName, request.PluginId, request.Name),
                Encoding.UTF8,
                cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(
                registryPath,
                BuildRegistryFile(projectName, pluginClassName, namespaceName, request.PluginId, request.Name),
                Encoding.UTF8,
                cancellationToken)
            .ConfigureAwait(false);

        return PluginScaffoldResult.Success(outputDirectory);
    }

    private static string BuildPluginContractsReference(string outputDirectory, string currentDirectory)
    {
        var repositoryRoot = FindRepositoryRoot(currentDirectory);
        if (repositoryRoot is null)
        {
            // Callora.Plugin.Sdk statt Callora.Core: Es bringt dieselbe Vertragsfläche mit,
            // dazu die Governance-Analyzer und die Build-Regel, die Plattform-Assemblies aus
            // dem Ausgabeordner hält. Vorher stand hier ExcludeAssets="runtime" von Hand —
            // eine Zeile, die ein Plugin-Autor beim Umbauen streicht, ohne dass etwas
            // fehlschlägt; der Bruch der Typidentität fällt erst beim Laden auf.
            // Die Version ist die der CLI: Beide kommen aus demselben Release, also
            // scaffoldet ein Werkzeug niemals gegen ein SDK, das es nicht gibt.
            return $"<PackageReference Include=\"Callora.Plugin.Sdk\" Version=\"{SdkPackageVersion()}\" />";
        }

        var relativeTo = new Func<string[], string>(segments => Path
            .GetRelativePath(outputDirectory, Path.Combine([repositoryRoot, .. segments]))
            .Replace('\\', '/'));

        // Im Repository selbst gibt es keine Pakete, also die Einzelteile, die das SDK
        // sonst bündelt — inklusive Analyzer, der hier bisher fehlte: Ein so erzeugtes
        // Plugin lief ohne CAL0001-CAL0003 und merkte einen Grenzübertritt nicht.
        return $"""
                <ProjectReference Include="{relativeTo(["src", "Core", "Callora.Core.csproj"])}" Private="false" ExcludeAssets="runtime" />
                    <ProjectReference Include="{relativeTo(["src", "Analyzers", "Callora.Analyzers.csproj"])}"
                                      OutputItemType="Analyzer"
                                      ReferenceOutputAssembly="false" />
                """;
    }

    /// <summary>
    /// Die Version dieser CLI, ohne Build-Metadaten — MinVer hängt "+&lt;sha&gt;" an, was in
    /// einer Paketversion nichts zu suchen hat.
    /// </summary>
    private static string SdkPackageVersion()
    {
        var informational = typeof(PluginScaffolder).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return "0.1.0";
        }

        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? informational : informational[..plus];
    }

    private static string? FindRepositoryRoot(string currentDirectory)
    {
        var directoryInfo = new DirectoryInfo(currentDirectory);
        while (directoryInfo is not null)
        {
            var solutionPath = Path.Combine(directoryInfo.FullName, "Callora.Host.sln");
            if (File.Exists(solutionPath))
            {
                return directoryInfo.FullName;
            }

            directoryInfo = directoryInfo.Parent;
        }

        return null;
    }

    private static string BuildProjectFile(string pluginContractsReference) =>
        $@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn>
    <!-- Only src/**/*.cs is compiled. Any front-end bundle (package.json / node_modules
         at the plugin root) stays out of the .NET compilation; source lives under src/. -->
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include=""src/**/*.cs"" />
  </ItemGroup>

  <ItemGroup>
    {pluginContractsReference}
  </ItemGroup>

  <ItemGroup>
    <None Include=""registry.json"">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
";

    private static string BuildPluginClass(
        string namespaceName,
        string className,
        string pluginId,
        string displayName) =>
        $@"using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins.Contracts;

namespace {namespaceName};

/// <summary>
/// Example host-managed plugin generated by the Callora scaffold CLI.
/// </summary>
public sealed class {className} : IHostManagedPlugin
{{
    public string PluginId => ""{pluginId}"";

    public string DisplayName => ""{displayName}"";

    public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}}
";

    private static string BuildRegistryFile(
        string projectName,
        string className,
        string namespaceName,
        string pluginId,
        string displayName)
    {
        var model = new
        {
            contractVersion = "v1",
            schemaVersion = "1.0",
            name = displayName,
            pluginId,
            version = "0.1.0",
            assemblyFileName = $"{projectName}.dll",
            entryTypeName = $"{namespaceName}.{className}",
            capabilities = new[] { "workspace.navigation" },
            extensions = new[]
            {
                new
                {
                    extensionPointId = "workspace.navigation.main",
                    surface = "surface"
                }
            },
            dependencies = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Callora.Core"] = ">=0.1.0"
            }
        };

        return JsonSerializer.Serialize(
            model,
            JsonOptions) + Environment.NewLine;
    }
}
