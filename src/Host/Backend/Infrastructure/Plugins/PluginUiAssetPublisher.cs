using System.Text.Json;
using Callora.Host.Backend.Application.Persistence;
using Callora.Host.Backend.Application.Plugins;
using Callora.Host.Backend.Domain.Plugins;
using Callora.Host.Backend.Infrastructure.Startup;
using Callora.Hosting.Application.Options;
using Microsoft.AspNetCore.Hosting;

namespace Callora.Host.Backend.Infrastructure.Plugins;

public sealed class PluginUiAssetPublisher(
    IPluginInstallationRepository installationRepository,
    IWebHostEnvironment environment,
    CalloraHostingOptions hostingOptions,
    ILogger<PluginUiAssetPublisher> logger) : IPluginUiAssetPublisher
{
    private static readonly string[] EntryCandidates =
    [
        "main.ts",
        "main.js",
        "index.ts",
        "index.js",
        "app.ts",
        "app.js",
        "src/main.ts",
        "src/main.js",
        "src/index.ts",
        "src/index.js",
        "src/app.ts",
        "src/app.js"
    ];
    private static readonly string[] StyleEntryCandidates =
    [
        "main.css",
        "style.css",
        "styles.css"
    ];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private static readonly JsonSerializerOptions RegistryJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task PublishAllAsync(CancellationToken cancellationToken = default)
    {
        var pluginAssetsRoot = Path.Combine(ResolveWebRootPath(), "plugin-assets");
        var buildDirectory = Path.Combine(pluginAssetsRoot, ".build");
        var manifestPath = Path.Combine(buildDirectory, "ui-assets.manifest.json");

        RecreateDirectory(pluginAssetsRoot);
        Directory.CreateDirectory(buildDirectory);

        var installations = await installationRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        var candidates = installations
            .Where(x => x.State == PluginInstallationState.Active)
            .Where(x => !string.IsNullOrWhiteSpace(x.PluginId))
            .OrderBy(x => x.PluginId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var pluginRootsByPluginId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var installation in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pluginId = installation.PluginId.Trim();
            var pluginRoot = ResolvePluginRoot(installation.AssemblyPath);
            if (pluginRoot is null)
            {
                logger.LogWarning(
                    "Skipping plugin UI asset publish for {PluginId}. Could not resolve plugin root from {AssemblyPath}.",
                    pluginId,
                    installation.AssemblyPath);
                continue;
            }

            pluginRootsByPluginId[pluginId] = pluginRoot;
        }

        var localPluginRoots = DiscoverLocalPluginRoots(cancellationToken);
        foreach (var pair in localPluginRoots)
        {
            if (!pluginRootsByPluginId.ContainsKey(pair.Key))
            {
                pluginRootsByPluginId[pair.Key] = pair.Value;
            }
        }

        var entries = new List<PluginUiAssetManifestEntry>();
        var styleEntries = new List<PluginUiStyleManifestEntry>();
        var workspaceTemplates = new List<PluginWorkspaceTemplateManifestEntry>();

        foreach (var pair in pluginRootsByPluginId.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pluginId = pair.Key;
            var pluginRoot = pair.Value;

            PublishSurface(
                pluginId,
                pluginRoot,
                "admin",
                pluginAssetsRoot,
                entries,
                styleEntries);

            PublishSurface(
                pluginId,
                pluginRoot,
                "workspace",
                pluginAssetsRoot,
                entries,
                styleEntries);

            PublishWorkspaceTemplates(
                pluginId,
                pluginRoot,
                pluginAssetsRoot,
                workspaceTemplates);
        }

        var manifest = new
        {
            generatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            entries = entries
                .OrderBy(x => x.PluginId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Surface, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.EntryPath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            styleEntries = styleEntries
                .OrderBy(x => x.PluginId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Surface, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.StylePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            workspaceTemplates = workspaceTemplates
                .OrderBy(x => x.PluginId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.TemplatePath, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };

        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await File.WriteAllTextAsync(manifestPath, json, cancellationToken).ConfigureAwait(false);
    }

    private string ResolveWebRootPath()
    {
        var webRoot = environment.WebRootPath;
        return string.IsNullOrWhiteSpace(webRoot)
            ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
            : webRoot;
    }

    private Dictionary<string, string> DiscoverLocalPluginRoots(CancellationToken cancellationToken)
    {
        var discovered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(hostingOptions.PluginDirectory))
        {
            return discovered;
        }

        var pluginDirectory = CalloraHostingPathResolver.ResolvePluginDirectory(hostingOptions.PluginDirectory);
        if (!Directory.Exists(pluginDirectory))
        {
            return discovered;
        }

        var registryFiles = Directory
            .EnumerateFiles(pluginDirectory, "registry.json", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var registryFile in registryFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pluginRoot = Path.GetDirectoryName(registryFile);
            if (string.IsNullOrWhiteSpace(pluginRoot) || !HasResourceRoots(pluginRoot))
            {
                continue;
            }

            var pluginId = ReadPluginIdFromRegistry(registryFile);
            if (string.IsNullOrWhiteSpace(pluginId))
            {
                pluginId = new DirectoryInfo(pluginRoot).Name;
            }

            var normalizedPluginId = pluginId.Trim();
            if (!discovered.ContainsKey(normalizedPluginId))
            {
                discovered[normalizedPluginId] = pluginRoot;
            }
        }

        return discovered;
    }

    private string? ReadPluginIdFromRegistry(string registryPath)
    {
        try
        {
            var json = File.ReadAllText(registryPath);
            var dto = JsonSerializer.Deserialize<PluginRegistryJsonDto>(json, RegistryJsonOptions);
            return dto?.PluginId;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Skipping invalid plugin registry while publishing UI assets: {RegistryPath}", registryPath);
            return null;
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not read plugin registry while publishing UI assets: {RegistryPath}", registryPath);
            return null;
        }
    }

    private static string? ResolvePluginRoot(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return null;
        }

        var fullAssemblyPath = Path.GetFullPath(assemblyPath);
        var current = new DirectoryInfo(Path.GetDirectoryName(fullAssemblyPath) ?? string.Empty);
        string? fallbackRoot = null;
        while (current is not null)
        {
            var registryPath = Path.Combine(current.FullName, "registry.json");
            if (File.Exists(registryPath))
            {
                if (HasResourceRoots(current.FullName))
                {
                    return current.FullName;
                }

                fallbackRoot ??= current.FullName;
            }

            current = current.Parent;
        }

        return fallbackRoot;
    }

    private static bool HasResourceRoots(string pluginRoot)
    {
        return
            Directory.Exists(Path.Combine(pluginRoot, "src", "Resources", "public")) ||
            Directory.Exists(Path.Combine(pluginRoot, "src", "Resources", "app")) ||
            Directory.Exists(Path.Combine(pluginRoot, "src", "Resources", "views", "workspace")) ||
            Directory.Exists(Path.Combine(pluginRoot, "public")) ||
            Directory.Exists(Path.Combine(pluginRoot, "app")) ||
            Directory.Exists(Path.Combine(pluginRoot, "views", "workspace"));
    }

    private void PublishSurface(
        string pluginId,
        string pluginRoot,
        string surface,
        string pluginAssetsRoot,
        ICollection<PluginUiAssetManifestEntry> manifestEntries,
        ICollection<PluginUiStyleManifestEntry> styleManifestEntries)
    {
        var sourceDirectory = ResolveSurfaceSourceDirectory(pluginRoot, surface);
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        var targetDirectory = ResolveContainedTargetDirectory(pluginAssetsRoot, pluginId, "app", surface);
        if (targetDirectory is null)
        {
            return;
        }
        var entry = ResolveEntryFile(sourceDirectory);
        var entryRelativePath = entry is null
            ? null
            : ToManifestPath(Path.GetRelativePath(sourceDirectory, entry));

        // ADR-011: Das Manifest referenziert nur finale Pfade. Liegt der Einstieg in
        // einem src/-Wrapper, wird dieser beim Publizieren aufgelöst (src/main.js -> main.js).
        if (entryRelativePath is not null &&
            entryRelativePath.StartsWith("src/", StringComparison.OrdinalIgnoreCase))
        {
            CopyDirectory(Path.Combine(sourceDirectory, "src"), targetDirectory);
            CopyDirectoryExcept(sourceDirectory, targetDirectory, "src");
            entryRelativePath = entryRelativePath["src/".Length..];
        }
        else
        {
            CopyDirectory(sourceDirectory, targetDirectory);
        }

        foreach (var styleCandidate in StyleEntryCandidates)
        {
            if (File.Exists(Path.Combine(targetDirectory, styleCandidate)))
            {
                var stylePath = ToManifestPath(Path.Combine(pluginId, "app", surface, styleCandidate));
                styleManifestEntries.Add(new PluginUiStyleManifestEntry(pluginId, surface, stylePath));
            }
        }

        if (entryRelativePath is null)
        {
            return;
        }

        var entryPath = ToManifestPath(Path.Combine(pluginId, "app", surface, entryRelativePath));
        manifestEntries.Add(new PluginUiAssetManifestEntry(pluginId, surface, entryPath));
    }

    private static void CopyDirectoryExcept(string sourceDirectory, string targetDirectory, string exceptSubdirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        Directory.CreateDirectory(targetDirectory);
        var excludedPrefix = exceptSubdirectory + Path.DirectorySeparatorChar;

        var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
        foreach (var sourceFile in files)
        {
            var relative = Path.GetRelativePath(sourceDirectory, sourceFile);
            if (relative.StartsWith(excludedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var targetFile = Path.Combine(targetDirectory, relative);
            var targetFolder = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrWhiteSpace(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            File.Copy(sourceFile, targetFile, overwrite: true);
        }
    }

    private void PublishWorkspaceTemplates(
        string pluginId,
        string pluginRoot,
        string pluginAssetsRoot,
        ICollection<PluginWorkspaceTemplateManifestEntry> templates)
    {
        var sourceDirectory = ResolveWorkspaceTemplateSourceDirectory(pluginRoot);
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        var targetDirectory = ResolveContainedTargetDirectory(pluginAssetsRoot, pluginId, "views", "workspace");
        if (targetDirectory is null)
        {
            return;
        }
        CopyDirectory(sourceDirectory, targetDirectory);

        var files = Directory
            .GetFiles(sourceDirectory, "*", SearchOption.AllDirectories)
            .Where(x => string.Equals(Path.GetExtension(x), ".html", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var templatePath = ToManifestPath(Path.Combine(pluginId, "views", "workspace", relative));
            templates.Add(new PluginWorkspaceTemplateManifestEntry(pluginId, templatePath));
        }
    }

    private static string? ResolveEntryFile(string sourceDirectory)
    {
        foreach (var candidate in EntryCandidates)
        {
            var candidatePath = Path.Combine(sourceDirectory, candidate);
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        return null;
    }

    private static string ToManifestPath(string value) =>
        value.Replace('\\', '/');

    /// <summary>
    /// Plugin ids come from registry.json files on disk — a crafted id like
    /// "../x" must never write outside the asset root.
    /// </summary>
    private string? ResolveContainedTargetDirectory(
        string pluginAssetsRoot,
        string pluginId,
        params string[] segments)
    {
        var root = Path.GetFullPath(pluginAssetsRoot);
        var target = Path.GetFullPath(Path.Combine([pluginAssetsRoot, pluginId, .. segments]));
        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Skipping plugin UI asset publish for {PluginId}: target path escapes the plugin asset root.",
                pluginId);
            return null;
        }

        return target;
    }

    private static string ResolveSurfaceSourceDirectory(string pluginRoot, string surface)
    {
        // Shopware-analog: compiled deliverables live under Resources/public;
        // the app/ directories carry sources and stay with the vendor. The
        // app fallbacks keep legacy layouts working.
        string[] candidates =
        [
            Path.Combine(pluginRoot, "src", "Resources", "public", surface),
            Path.Combine(pluginRoot, "public", surface),
            Path.Combine(pluginRoot, "src", "Resources", "app", surface),
            Path.Combine(pluginRoot, "app", surface)
        ];

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[^1];
    }

    private static string ResolveWorkspaceTemplateSourceDirectory(string pluginRoot)
    {
        var sourcePreferred = Path.Combine(pluginRoot, "src", "Resources", "views", "workspace");
        if (Directory.Exists(sourcePreferred))
        {
            return sourcePreferred;
        }

        return Path.Combine(pluginRoot, "views", "workspace");
    }

    private static void RecreateDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        Directory.CreateDirectory(targetDirectory);

        var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
        foreach (var sourceFile in files)
        {
            var relative = Path.GetRelativePath(sourceDirectory, sourceFile);
            var targetFile = Path.Combine(targetDirectory, relative);
            var targetFolder = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrWhiteSpace(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            File.Copy(sourceFile, targetFile, overwrite: true);
        }
    }

}
