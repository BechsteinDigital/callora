using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Options;
using Callora.Core.Application.Persistence;
using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;
using Callora.Core.Infrastructure.Plugins;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Callora.Core.Infrastructure.Startup;

/// <summary>
/// Scans the local plugin directories and reconciles them against the installation
/// registry (Shopware <c>plugin:refresh</c> equivalent). Registers new plugins,
/// updates changed manifests, and — for plugins whose assembly disappeared and that
/// originate from a scan root — uninstalls the inactive ones and reports the active
/// ones. Runs unconditionally on demand; the startup gate lives in the hosted service.
/// </summary>
internal sealed class LocalPluginDiscoveryService(
    CalloraHostingOptions hostingOptions,
    IPluginInstallationRepository installationRepository,
    IPluginLifecycleService lifecycleService,
    ILocalPluginProjectBuilder projectBuilder,
    ILogger<LocalPluginDiscoveryService> logger) : IPluginDiscoveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<PluginDiscoveryRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var scanRoots = BuildScanRoots().Where(root => Directory.Exists(root.Directory)).ToArray();

        var discovered = new Dictionary<string, DiscoveredPlugin>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in scanRoots)
        {
            foreach (var registryFile in Directory
                .EnumerateFiles(root.Directory, "registry.json", SearchOption.AllDirectories)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var plugin = await ResolveDiscoveredAsync(registryFile, root.DefaultTier, cancellationToken).ConfigureAwait(false);
                if (plugin is not null && !discovered.ContainsKey(plugin.PluginId))
                {
                    discovered[plugin.PluginId] = plugin;
                }
            }
        }

        var installations = await installationRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        var byId = installations.ToDictionary(installation => installation.PluginId, StringComparer.OrdinalIgnoreCase);

        var added = new List<string>();
        var updated = new List<string>();
        var removedInactive = new List<string>();
        var missingActive = new List<string>();

        foreach (var plugin in discovered.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!byId.TryGetValue(plugin.PluginId, out var existing) || existing.State == PluginInstallationState.Uninstalled)
            {
                if (await TryInstallAsync(plugin, cancellationToken).ConfigureAwait(false))
                {
                    added.Add(plugin.PluginId);
                }
            }
            else if (HasManifestChanged(existing, plugin)
                && await TryUpdateAsync(plugin.PluginId, cancellationToken).ConfigureAwait(false))
            {
                updated.Add(plugin.PluginId);
            }
        }

        // Plugins that vanished from the file system — only those originating from a
        // scan root, so NuGet-/operator-installed plugins elsewhere are left untouched.
        foreach (var installation in installations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (installation.State == PluginInstallationState.Uninstalled
                || discovered.ContainsKey(installation.PluginId)
                || !IsUnderScanRoots(installation.AssemblyPath, scanRoots))
            {
                continue;
            }

            if (installation.State == PluginInstallationState.Active)
            {
                logger.LogWarning(
                    "Plugin {PluginId} is active but its assembly is missing at {AssemblyPath}; kept installed.",
                    installation.PluginId,
                    installation.AssemblyPath);
                missingActive.Add(installation.PluginId);
            }
            else if (await TryUninstallAsync(installation.PluginId, cancellationToken).ConfigureAwait(false))
            {
                removedInactive.Add(installation.PluginId);
            }
        }

        return new PluginDiscoveryRefreshResult(added, updated, removedInactive, missingActive);
    }

    private async Task<bool> TryInstallAsync(DiscoveredPlugin plugin, CancellationToken cancellationToken)
    {
        var result = await lifecycleService
            .InstallAsync(new InstallPluginCommand(plugin.AssemblyPath, plugin.EntryTypeName, "system:plugin-refresh"), cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            logger.LogWarning("Plugin refresh could not register {PluginId} from {AssemblyPath}: {Message}", plugin.PluginId, plugin.AssemblyPath, result.Message);
        }

        return result.IsSuccess;
    }

    private async Task<bool> TryUpdateAsync(string pluginId, CancellationToken cancellationToken)
    {
        var result = await lifecycleService
            .UpdateFromLocalAsync(new UpdateLocalPluginCommand(pluginId, BuildIfNeeded: false, ForceBuild: false, RequestedBy: "system:plugin-refresh"), cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            logger.LogWarning("Plugin refresh could not update {PluginId}: {Message}", pluginId, result.Message);
        }

        return result.IsSuccess;
    }

    private async Task<bool> TryUninstallAsync(string pluginId, CancellationToken cancellationToken)
    {
        var result = await lifecycleService
            .UninstallAsync(new PluginLifecycleCommand(pluginId, "system:plugin-refresh", WorkspaceKey: null), cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            logger.LogWarning("Plugin refresh could not uninstall missing plugin {PluginId}: {Message}", pluginId, result.Message);
        }

        return result.IsSuccess;
    }

    private static bool HasManifestChanged(PluginInstallation existing, DiscoveredPlugin plugin)
        => !string.Equals(existing.EntryTypeName, plugin.EntryTypeName, StringComparison.Ordinal)
           || !string.Equals(Path.GetFullPath(existing.AssemblyPath), plugin.AssemblyPath, StringComparison.OrdinalIgnoreCase);

    private async Task<DiscoveredPlugin?> ResolveDiscoveredAsync(string registryFile, PluginTier defaultTier, CancellationToken cancellationToken)
    {
        var manifest = await ReadManifestAsync(registryFile, cancellationToken).ConfigureAwait(false);
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.PluginId) || string.IsNullOrWhiteSpace(manifest.AssemblyFileName))
        {
            logger.LogWarning("Skipping plugin registry '{RegistryFile}' because pluginId or assemblyFileName is missing.", registryFile);
            return null;
        }

        var pluginRoot = Path.GetDirectoryName(registryFile);
        if (string.IsNullOrWhiteSpace(pluginRoot))
        {
            return null;
        }

        var assemblyPath = ResolveAssemblyPath(pluginRoot, manifest.AssemblyFileName);
        if (assemblyPath is null)
        {
            var projectPath = ResolveProjectPath(pluginRoot);
            if (projectPath is null)
            {
                logger.LogWarning(
                    "Skipping plugin '{PluginId}'. No precompiled assembly '{AssemblyFileName}' and no csproj in '{PluginRoot}'.",
                    manifest.PluginId, manifest.AssemblyFileName, pluginRoot);
                return null;
            }

            var build = await projectBuilder.BuildAsync(projectPath, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!build.IsSuccess)
            {
                logger.LogWarning("Skipping plugin '{PluginId}'. Project build failed for '{ProjectPath}': {Message}", manifest.PluginId, projectPath, build.Message);
                return null;
            }

            assemblyPath = ResolveAssemblyPath(pluginRoot, manifest.AssemblyFileName);
            if (assemblyPath is null)
            {
                logger.LogWarning("Skipping plugin '{PluginId}'. Build succeeded but assembly '{AssemblyFileName}' was not found.", manifest.PluginId, manifest.AssemblyFileName);
                return null;
            }
        }

        return new DiscoveredPlugin(manifest.PluginId.Trim(), assemblyPath, manifest.EntryTypeName);
    }

    private IReadOnlyList<(string Directory, PluginTier DefaultTier)> BuildScanRoots()
    {
        var roots = new List<(string Directory, PluginTier DefaultTier)>();
        if (!string.IsNullOrWhiteSpace(hostingOptions.StaticPluginDirectory))
        {
            roots.Add((CalloraHostingPathResolver.ResolvePluginDirectory(hostingOptions.StaticPluginDirectory), PluginTier.System));
        }

        if (!string.IsNullOrWhiteSpace(hostingOptions.PluginDirectory))
        {
            roots.Add((CalloraHostingPathResolver.ResolvePluginDirectory(hostingOptions.PluginDirectory), PluginTier.Application));
        }

        return roots;
    }

    private static bool IsUnderScanRoots(string assemblyPath, IReadOnlyList<(string Directory, PluginTier DefaultTier)> scanRoots)
    {
        string full;
        try
        {
            full = Path.GetFullPath(assemblyPath);
        }
        catch (ArgumentException)
        {
            return false;
        }

        return scanRoots.Any(root =>
        {
            var rootFull = Path.GetFullPath(root.Directory);
            return full.StartsWith(rootFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        });
    }

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

        var candidate = Directory
            .EnumerateFiles(binDirectory, assemblyFileName, SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();
        return candidate?.FullName;
    }

    private static string? ResolveProjectPath(string pluginRoot)
        => Directory
            .EnumerateFiles(pluginRoot, "*.csproj", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
}
