using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Options;
using Callora.Core.Application.Plugins;
using Callora.Core.Infrastructure.Startup;
using System.Text.Json;

namespace Callora.Core.Infrastructure.Plugins;

public sealed class LocalPluginInstallSourceResolver(
    CalloraHostingOptions hostingOptions,
    ILocalPluginProjectBuilder projectBuilder) : ILocalPluginInstallSourceResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<LocalPluginInstallSourceResolveResult> ResolveForInstallAsync(
        string pluginId,
        bool buildIfNeeded,
        bool forceBuild = false,
        CancellationToken cancellationToken = default)
    {
        var normalizedPluginId = pluginId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedPluginId))
        {
            return new LocalPluginInstallSourceResolveResult(
                false,
                normalizedPluginId,
                null,
                null,
                false,
                "Plugin ID ist erforderlich.",
                PluginLifecycleErrorCodes.LocalPluginIdMissing);
        }

        var pluginDirectory = CalloraHostingPathResolver.ResolvePluginDirectory(hostingOptions.PluginDirectory);
        if (string.IsNullOrWhiteSpace(pluginDirectory) || !Directory.Exists(pluginDirectory))
        {
            return new LocalPluginInstallSourceResolveResult(
                false,
                normalizedPluginId,
                null,
                null,
                false,
                $"Plugin-Verzeichnis '{pluginDirectory}' wurde nicht gefunden.",
                PluginLifecycleErrorCodes.LocalPluginDirectoryMissing);
        }

        var match = await FindRegistryByPluginIdAsync(pluginDirectory, normalizedPluginId, cancellationToken).ConfigureAwait(false);
        if (match is null)
        {
            return new LocalPluginInstallSourceResolveResult(
                false,
                normalizedPluginId,
                null,
                null,
                false,
                $"Lokales Plugin '{normalizedPluginId}' wurde nicht gefunden.",
                PluginLifecycleErrorCodes.LocalPluginNotFound);
        }

        var dto = match.Registry;
        if (string.IsNullOrWhiteSpace(dto.AssemblyFileName))
        {
            return new LocalPluginInstallSourceResolveResult(
                false,
                normalizedPluginId,
                null,
                null,
                false,
                $"registry.json für '{normalizedPluginId}' enthält kein assemblyFileName.",
                PluginLifecycleErrorCodes.LocalPluginRegistryInvalid);
        }

        var pluginRoot = Path.GetDirectoryName(match.RegistryPath) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pluginRoot))
        {
            return new LocalPluginInstallSourceResolveResult(
                false,
                normalizedPluginId,
                null,
                null,
                false,
                $"Plugin-Wurzel für '{normalizedPluginId}' konnte nicht aufgelöst werden.",
                PluginLifecycleErrorCodes.LocalPluginRegistryInvalid);
        }

        var assemblyPath = ResolveAssemblyPath(pluginRoot, dto.AssemblyFileName);
        if (!forceBuild && !string.IsNullOrWhiteSpace(assemblyPath))
        {
            return new LocalPluginInstallSourceResolveResult(
                true,
                normalizedPluginId,
                assemblyPath,
                dto.EntryTypeName,
                false,
                "Lokale Plugin-DLL gefunden.");
        }

        if (!buildIfNeeded)
        {
            return new LocalPluginInstallSourceResolveResult(
                false,
                normalizedPluginId,
                null,
                dto.EntryTypeName,
                false,
                $"Für '{normalizedPluginId}' wurde keine DLL gefunden. Build ist deaktiviert.",
                PluginLifecycleErrorCodes.LocalPluginBuildRequired);
        }

        var projectPath = ResolveProjectPath(pluginRoot);
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return new LocalPluginInstallSourceResolveResult(
                false,
                normalizedPluginId,
                null,
                dto.EntryTypeName,
                false,
                $"Für '{normalizedPluginId}' wurde keine csproj-Datei gefunden.",
                PluginLifecycleErrorCodes.LocalPluginProjectMissing);
        }

        var buildResult = await projectBuilder.BuildAsync(projectPath, forceRebuild: forceBuild, cancellationToken).ConfigureAwait(false);
        if (!buildResult.IsSuccess)
        {
            return new LocalPluginInstallSourceResolveResult(
                false,
                normalizedPluginId,
                null,
                dto.EntryTypeName,
                true,
                $"Build für '{normalizedPluginId}' fehlgeschlagen: {buildResult.Message}",
                PluginLifecycleErrorCodes.LocalPluginBuildFailed);
        }

        assemblyPath = forceBuild
            ? ResolveAssemblyPathFromBuildOutput(pluginRoot, dto.AssemblyFileName)
            : ResolveAssemblyPath(pluginRoot, dto.AssemblyFileName);
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return new LocalPluginInstallSourceResolveResult(
                false,
                normalizedPluginId,
                null,
                dto.EntryTypeName,
                true,
                $"Build war erfolgreich, aber DLL '{dto.AssemblyFileName}' wurde nicht gefunden.",
                PluginLifecycleErrorCodes.LocalPluginAssemblyMissingAfterBuild);
        }

        return new LocalPluginInstallSourceResolveResult(
            true,
            normalizedPluginId,
            assemblyPath,
            dto.EntryTypeName,
            true,
            "Lokales Plugin wurde kompiliert und DLL aufgelöst.");
    }

    private static async Task<PluginRegistryMatch?> FindRegistryByPluginIdAsync(
        string pluginDirectory,
        string pluginId,
        CancellationToken cancellationToken)
    {
        var registryFiles = Directory.EnumerateFiles(pluginDirectory, "registry.json", SearchOption.AllDirectories);

        foreach (var registryFile in registryFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dto = await TryReadRegistryAsync(registryFile, cancellationToken).ConfigureAwait(false);
            if (dto is null || string.IsNullOrWhiteSpace(dto.PluginId))
            {
                continue;
            }

            if (string.Equals(dto.PluginId.Trim(), pluginId, StringComparison.OrdinalIgnoreCase))
            {
                return new PluginRegistryMatch(registryFile, dto);
            }
        }

        return null;
    }

    private static async Task<PluginRegistryJsonDto?> TryReadRegistryAsync(
        string registryFile,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(registryFile, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<PluginRegistryJsonDto>(json, JsonOptions);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ResolveAssemblyPath(string pluginRoot, string assemblyFileName)
    {
        var directAssemblyPath = Path.Combine(pluginRoot, assemblyFileName);
        if (File.Exists(directAssemblyPath))
        {
            return Path.GetFullPath(directAssemblyPath);
        }

        var binDirectory = Path.Combine(pluginRoot, "bin");
        if (!Directory.Exists(binDirectory))
        {
            return null;
        }

        var candidates = Directory
            .EnumerateFiles(binDirectory, assemblyFileName, SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToArray();

        return candidates.Length == 0
            ? null
            : candidates[0].FullName;
    }

    private static string? ResolveAssemblyPathFromBuildOutput(string pluginRoot, string assemblyFileName)
    {
        var binDirectory = Path.Combine(pluginRoot, "bin");
        if (!Directory.Exists(binDirectory))
        {
            return null;
        }

        var candidates = Directory
            .EnumerateFiles(binDirectory, assemblyFileName, SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToArray();

        return candidates.Length == 0
            ? null
            : candidates[0].FullName;
    }

    private static string? ResolveProjectPath(string pluginRoot)
    {
        var csprojFiles = Directory
            .EnumerateFiles(pluginRoot, "*.csproj", SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return csprojFiles.Length == 0
            ? null
            : csprojFiles[0];
    }
}
