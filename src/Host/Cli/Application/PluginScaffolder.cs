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
            // Compile against the host contracts (now part of Callora.Core) but do
            // not ship Core with the plugin — the host provides it, the plugin ALC
            // shares its type identity (REV2 §10.1A).
            return "<PackageReference Include=\"Callora.Core\" Version=\"0.1.0\" ExcludeAssets=\"runtime\" />";
        }

        var projectReferenceAbsolutePath = Path.Combine(
            repositoryRoot,
            "src",
            "Core",
            "Callora.Core.csproj");

        var relativePath = Path.GetRelativePath(outputDirectory, projectReferenceAbsolutePath)
            .Replace('\\', '/');

        return $"<ProjectReference Include=\"{relativePath}\" Private=\"false\" ExcludeAssets=\"runtime\" />";
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
