using System.Text.Json;
using Callora.Host.Backend.Application.Persistence;
using Callora.Host.Backend.Application.Plugins;
using Callora.Host.Backend.Application.Lifecycle;
using Callora.Hosting.Application.Options;
using Callora.Host.Backend.Infrastructure.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Callora.Host.Backend.Infrastructure.Startup;

public sealed class LocalPluginDiscoveryHostedService(
    IServiceProvider services,
    CalloraHostingOptions hostingOptions,
    ILocalPluginProjectBuilder projectBuilder,
    ILogger<LocalPluginDiscoveryHostedService> logger) : IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!hostingOptions.AutoLoadPlugins || string.IsNullOrWhiteSpace(hostingOptions.PluginDirectory))
        {
            return;
        }

        var pluginDirectory = CalloraHostingPathResolver.ResolvePluginDirectory(hostingOptions.PluginDirectory);
        if (!Directory.Exists(pluginDirectory))
        {
            logger.LogInformation("Plugin directory '{PluginDirectory}' does not exist. Skipping local plugin discovery.", pluginDirectory);
            return;
        }

        var registryFiles = Directory
            .EnumerateFiles(pluginDirectory, "registry.json", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (registryFiles.Length == 0)
        {
            return;
        }

        using var scope = services.CreateScope();
        var installationRepository = scope.ServiceProvider.GetRequiredService<IPluginInstallationRepository>();
        var lifecycleService = scope.ServiceProvider.GetRequiredService<IPluginLifecycleService>();

        foreach (var registryFile in registryFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var manifest = await ReadManifestAsync(registryFile, cancellationToken).ConfigureAwait(false);
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.PluginId) || string.IsNullOrWhiteSpace(manifest.AssemblyFileName))
            {
                logger.LogWarning("Skipping plugin registry '{RegistryFile}' because pluginId or assemblyFileName is missing.", registryFile);
                continue;
            }

            var pluginId = manifest.PluginId.Trim();
            var existing = await installationRepository.GetByPluginIdAsync(pluginId, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                continue;
            }

            var pluginRoot = Path.GetDirectoryName(registryFile);
            if (string.IsNullOrWhiteSpace(pluginRoot))
            {
                continue;
            }

            var assemblyPath = ResolveAssemblyPath(pluginRoot, manifest.AssemblyFileName);
            if (assemblyPath is null)
            {
                var projectPath = ResolveProjectPath(pluginRoot);
                if (projectPath is null)
                {
                    logger.LogWarning(
                        "Skipping plugin '{PluginId}'. No precompiled assembly '{AssemblyFileName}' and no csproj found in '{PluginRoot}'.",
                        pluginId,
                        manifest.AssemblyFileName,
                        pluginRoot);
                    continue;
                }

                var buildResult = await projectBuilder.BuildAsync(projectPath, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!buildResult.IsSuccess)
                {
                    logger.LogWarning(
                        "Skipping plugin '{PluginId}'. Project build failed for '{ProjectPath}': {Message}",
                        pluginId,
                        projectPath,
                        buildResult.Message);
                    continue;
                }

                assemblyPath = ResolveAssemblyPath(pluginRoot, manifest.AssemblyFileName);
                if (assemblyPath is null)
                {
                    logger.LogWarning(
                        "Skipping plugin '{PluginId}'. Build succeeded but assembly '{AssemblyFileName}' was not found.",
                        pluginId,
                        manifest.AssemblyFileName);
                    continue;
                }
            }

            var installResult = await lifecycleService
                .InstallAsync(
                    new InstallPluginCommand(
                        AssemblyPath: assemblyPath,
                        EntryTypeName: manifest.EntryTypeName,
                        RequestedBy: "system:startup-discovery"),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!installResult.IsSuccess)
            {
                logger.LogWarning(
                    "Local plugin auto-install failed for '{PluginId}' from '{AssemblyPath}': {Message}",
                    pluginId,
                    assemblyPath,
                    installResult.Message);
                continue;
            }

            if (!hostingOptions.AutoActivateInstalledPlugins || string.IsNullOrWhiteSpace(installResult.PluginId))
            {
                continue;
            }

            var activateResult = await lifecycleService
                .ActivateAsync(
                    new PluginLifecycleCommand(
                        installResult.PluginId,
                        RequestedBy: "system:startup-discovery",
                        WorkspaceKey: null),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!activateResult.IsSuccess)
            {
                logger.LogWarning(
                    "Local plugin auto-activation failed for '{PluginId}': {Message}",
                    installResult.PluginId,
                    activateResult.Message);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<PluginRegistryJsonDto?> ReadManifestAsync(string registryFile, CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(registryFile, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<PluginRegistryJsonDto>(json, JsonOptions);
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
