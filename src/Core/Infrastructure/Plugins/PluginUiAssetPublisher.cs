using Callora.Core.Application.Options;
using Callora.Core.Application.Persistence;
using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;
using Callora.Core.Infrastructure.Startup;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;

namespace Callora.Core.Infrastructure.Plugins;

public sealed class PluginUiAssetPublisher(
    IPluginInstallationRepository installationRepository,
    IWebHostEnvironment environment,
    CalloraHostingOptions hostingOptions,
    ILogger<PluginUiAssetPublisher> logger) : IPluginUiAssetPublisher
{
    // Only built JavaScript is a valid entry — the browser loader loads .js/.mjs
    // exclusively. Listing a .ts source entry would land in the manifest but be
    // silently ignored by the client, so TypeScript sources are NOT entry
    // candidates; they are detected separately only to warn about an unbuilt plugin.
    private static readonly string[] EntryCandidates =
    [
        "main.js",
        "main.mjs",
        "index.js",
        "index.mjs",
        "app.js",
        "app.mjs",
        "src/main.js",
        "src/main.mjs",
        "src/index.js",
        "src/index.mjs",
        "src/app.js",
        "src/app.mjs"
    ];
    private static readonly string[] TypeScriptEntryCandidates =
    [
        "main.ts",
        "index.ts",
        "app.ts",
        "src/main.ts",
        "src/index.ts",
        "src/app.ts"
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
        var webRootPath = ResolveWebRootPath();
        var pluginAssetsRoot = Path.Combine(webRootPath, "plugin-assets");
        // Build into a staging directory so the live assets keep serving until the
        // new set is complete; a crash mid-build leaves the previous publish intact
        // instead of an empty asset root. Dot-prefixed → excluded from static serving.
        var stagingRoot = Path.Combine(webRootPath, ".plugin-assets-staging");
        var backupRoot = Path.Combine(webRootPath, ".plugin-assets-old");
        var buildDirectory = Path.Combine(stagingRoot, ".build");
        var manifestPath = Path.Combine(buildDirectory, "ui-assets.manifest.json");

        RecreateDirectory(stagingRoot);
        Directory.CreateDirectory(buildDirectory);

        var installations = await installationRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        var candidates = installations
            .Where(x => x.State == PluginInstallationState.Active)
            .Where(x => !string.IsNullOrWhiteSpace(x.PluginId))
            .OrderBy(x => x.PluginId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // A plugin that is installed but not active must not be resurrected from
        // disk by local discovery below; only genuinely dev-only plugins (no
        // installation record at all) are served without an active record.
        var suppressedPluginIds = installations
            .Where(x => x.State != PluginInstallationState.Active)
            .Where(x => !string.IsNullOrWhiteSpace(x.PluginId))
            .Select(x => x.PluginId.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
            if (suppressedPluginIds.Contains(pair.Key))
            {
                continue;
            }

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
                stagingRoot,
                entries,
                styleEntries);

            PublishSurface(
                pluginId,
                pluginRoot,
                "workspace",
                stagingRoot,
                entries,
                styleEntries);

            PublishWorkspaceTemplates(
                pluginId,
                pluginRoot,
                stagingRoot,
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

        // Swap the completed staging set in for the live one. The manifest and its
        // assets move together, so a client never sees a manifest that references
        // not-yet-copied assets; the exposure window is two renames rather than a
        // full delete-then-copy.
        SwapPublishedAssets(pluginAssetsRoot, stagingRoot, backupRoot);
    }

    private static void SwapPublishedAssets(string liveRoot, string stagingRoot, string backupRoot)
    {
        // Recovery note: a crash between the two Moves below leaves no live root but a
        // populated backup. The next run rebuilds staging from scratch and this method
        // runs again — it deletes that backup first (losing only the PREVIOUS assets,
        // never the freshly built ones) and moves the new staging into place. The state
        // stays consistent; there is no partial-merge and no loss of the new build.
        if (Directory.Exists(backupRoot))
        {
            Directory.Delete(backupRoot, recursive: true);
        }

        // Move the live set aside (a rename, not a copy), then move staging into
        // place. Same volume (siblings under wwwroot) → each Move is an atomic rename.
        if (Directory.Exists(liveRoot))
        {
            Directory.Move(liveRoot, backupRoot);
        }

        Directory.Move(stagingRoot, liveRoot);

        if (Directory.Exists(backupRoot))
        {
            Directory.Delete(backupRoot, recursive: true);
        }
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

        // Mirror the discovery service (LocalPluginDiscoveryHostedService): the
        // System-tier static-plugin root is scanned alongside the application
        // root, so a dev-only static plugin (e.g. Communication) serves its UI
        // source before it is installed.
        foreach (var configuredDirectory in new[] { hostingOptions.StaticPluginDirectory, hostingOptions.PluginDirectory })
        {
            if (string.IsNullOrWhiteSpace(configuredDirectory))
            {
                continue;
            }

            var directory = CalloraHostingPathResolver.ResolvePluginDirectory(configuredDirectory);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            var registryFiles = Directory
                .EnumerateFiles(directory, "registry.json", SearchOption.AllDirectories)
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
            var styleFile = Path.Combine(targetDirectory, styleCandidate);
            if (File.Exists(styleFile))
            {
                var stylePath = ToManifestPath(Path.Combine(pluginId, "app", surface, styleCandidate));
                styleManifestEntries.Add(new PluginUiStyleManifestEntry(pluginId, surface, stylePath)
                {
                    ContentHash = TryComputeContentHash(styleFile)
                });
            }
        }

        if (entryRelativePath is null)
        {
            // A source dir with a TypeScript entry but no built .js means an unbuilt
            // plugin — its UI would never load. Make that diagnosable rather than silent.
            if (HasTypeScriptEntry(sourceDirectory))
            {
                logger.LogWarning(
                    "Plugin {PluginId} surface '{Surface}' has a TypeScript entry but no built JavaScript entry; its UI will not load. Build the plugin (Resources/public) before publishing.",
                    pluginId,
                    surface);
            }

            return;
        }

        var entryPath = ToManifestPath(Path.Combine(pluginId, "app", surface, entryRelativePath));
        manifestEntries.Add(new PluginUiAssetManifestEntry(pluginId, surface, entryPath)
        {
            ContentHash = TryComputeContentHash(Path.Combine(targetDirectory, entryRelativePath))
        });
    }

    /// <summary>
    /// A short content hash (first 8 bytes of SHA-256, lowercase hex) of a published
    /// asset, used purely for cache-busting — not a security digest. Null on read
    /// failure so publishing never breaks over an unreadable file; the client then
    /// falls back to a bare URL + revalidation.
    /// </summary>
    private static string? TryComputeContentHash(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var hash = System.Security.Cryptography.SHA256.HashData(stream);
            return Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool HasTypeScriptEntry(string sourceDirectory) =>
        TypeScriptEntryCandidates.Any(candidate => File.Exists(Path.Combine(sourceDirectory, candidate)));

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
