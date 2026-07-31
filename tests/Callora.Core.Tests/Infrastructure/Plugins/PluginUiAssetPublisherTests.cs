using Callora.Core.Application.Options;
using Callora.Core.Domain.Plugins;
using Callora.Core.Infrastructure.Plugins;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Callora.Core.Tests.Infrastructure.Plugins;

public sealed class PluginUiAssetPublisherTests
{
    [Fact]
    public async Task PublishAllAsync_LocalRegistryPluginsWithoutInstall_ArePublished()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"callora-plugin-assets-{Guid.NewGuid():N}");
        var pluginDirectory = Path.Combine(tempRoot, "custom", "plugins");
        var webRoot = Path.Combine(tempRoot, "wwwroot");
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(webRoot);

        try
        {
            CreatePlugin(
                pluginDirectory,
                pluginFolderName: "TemplateAlpha",
                pluginId: "template-alpha",
                createAdmin: true,
                createWorkspace: true,
                createWorkspaceTemplate: false);
            CreatePlugin(
                pluginDirectory,
                pluginFolderName: "TemplateBeta",
                pluginId: "template-beta",
                createAdmin: true,
                createWorkspace: false,
                createWorkspaceTemplate: true);

            var installations = new InMemoryPluginInstallationRepository();
            var environment = new TestWebHostEnvironment
            {
                WebRootPath = webRoot,
                ContentRootPath = tempRoot
            };
            var hostingOptions = new CalloraHostingOptions
            {
                PluginDirectory = pluginDirectory
            };

            var sut = new PluginUiAssetPublisher(
                installations,
                environment,
                hostingOptions,
                NullLogger<PluginUiAssetPublisher>.Instance);

            await sut.PublishAllAsync();

            var manifestPath = Path.Combine(webRoot, "plugin-assets", ".build", "ui-assets.manifest.json");
            Assert.True(File.Exists(manifestPath));

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
            var entries = document.RootElement.GetProperty("entries").EnumerateArray().ToArray();
            var pluginIds = entries
                .Select(x => x.GetProperty("pluginId").GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.Contains("template-alpha", pluginIds);
            Assert.Contains("template-beta", pluginIds);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PublishAllAsync_StaticPluginWithoutInstall_IsPublished()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"callora-plugin-assets-{Guid.NewGuid():N}");
        var staticPluginDirectory = Path.Combine(tempRoot, "custom", "static-plugins");
        var webRoot = Path.Combine(tempRoot, "wwwroot");
        Directory.CreateDirectory(staticPluginDirectory);
        Directory.CreateDirectory(webRoot);

        try
        {
            CreatePlugin(
                staticPluginDirectory,
                pluginFolderName: "Communication",
                pluginId: "communication",
                createAdmin: true,
                createWorkspace: true,
                createWorkspaceTemplate: false);

            var sut = new PluginUiAssetPublisher(
                new InMemoryPluginInstallationRepository(),
                new TestWebHostEnvironment { WebRootPath = webRoot, ContentRootPath = tempRoot },
                new CalloraHostingOptions { StaticPluginDirectory = staticPluginDirectory },
                NullLogger<PluginUiAssetPublisher>.Instance);

            await sut.PublishAllAsync();

            var manifestPath = Path.Combine(webRoot, "plugin-assets", ".build", "ui-assets.manifest.json");
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
            var pluginIds = document.RootElement.GetProperty("entries").EnumerateArray()
                .Select(x => x.GetProperty("pluginId").GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.Contains("communication", pluginIds);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PublishAllAsync_InstalledAndDiscoveredPlugin_IsNotDuplicated()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"callora-plugin-assets-{Guid.NewGuid():N}");
        var pluginDirectory = Path.Combine(tempRoot, "custom", "plugins");
        var webRoot = Path.Combine(tempRoot, "wwwroot");
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(webRoot);

        try
        {
            var pluginRoot = CreatePlugin(
                pluginDirectory,
                pluginFolderName: "TemplateAlpha",
                pluginId: "template-alpha",
                createAdmin: true,
                createWorkspace: false,
                createWorkspaceTemplate: false);

            var assemblyPath = Path.Combine(pluginRoot, "bin", "Release", "net10.0", "Callora.Plugins.TemplateAlpha.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath)!);
            await File.WriteAllTextAsync(assemblyPath, "placeholder");

            var installations = new InMemoryPluginInstallationRepository();
            var nowUtc = DateTimeOffset.UtcNow;
            var installed = PluginInstallation.CreateInstalled(
                pluginId: "template-alpha",
                displayName: "Template Alpha",
                assemblyPath,
                entryTypeName: null,
                nowUtc);
            installed.MarkActivated(nowUtc);
            await installations.AddAsync(installed);

            var environment = new TestWebHostEnvironment
            {
                WebRootPath = webRoot,
                ContentRootPath = tempRoot
            };
            var hostingOptions = new CalloraHostingOptions
            {
                PluginDirectory = pluginDirectory
            };

            var sut = new PluginUiAssetPublisher(
                installations,
                environment,
                hostingOptions,
                NullLogger<PluginUiAssetPublisher>.Instance);

            await sut.PublishAllAsync();

            var manifestPath = Path.Combine(webRoot, "plugin-assets", ".build", "ui-assets.manifest.json");
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
            var entries = document.RootElement.GetProperty("entries").EnumerateArray().ToArray();
            var templateAlphaEntries = entries
                .Count(x => string.Equals(
                    x.GetProperty("pluginId").GetString(),
                    "template-alpha",
                    StringComparison.OrdinalIgnoreCase));

            Assert.Equal(1, templateAlphaEntries);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PublishAllAsync_DeactivatedLocalPlugin_IsNotResurrectedFromDisk()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"callora-plugin-assets-{Guid.NewGuid():N}");
        var pluginDirectory = Path.Combine(tempRoot, "custom", "plugins");
        var webRoot = Path.Combine(tempRoot, "wwwroot");
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(webRoot);

        try
        {
            var pluginRoot = CreatePlugin(
                pluginDirectory,
                pluginFolderName: "TemplateAlpha",
                pluginId: "template-alpha",
                createAdmin: true,
                createWorkspace: false,
                createWorkspaceTemplate: false);

            var assemblyPath = Path.Combine(pluginRoot, "bin", "Release", "net10.0", "Callora.Plugins.TemplateAlpha.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath)!);
            await File.WriteAllTextAsync(assemblyPath, "placeholder");

            var installations = new InMemoryPluginInstallationRepository();
            var nowUtc = DateTimeOffset.UtcNow;
            var installed = PluginInstallation.CreateInstalled(
                pluginId: "template-alpha",
                displayName: "Template Alpha",
                assemblyPath,
                entryTypeName: null,
                nowUtc);
            installed.MarkActivated(nowUtc);
            installed.MarkDeactivated(nowUtc);
            await installations.AddAsync(installed);

            var sut = new PluginUiAssetPublisher(
                installations,
                new TestWebHostEnvironment { WebRootPath = webRoot, ContentRootPath = tempRoot },
                new CalloraHostingOptions { PluginDirectory = pluginDirectory },
                NullLogger<PluginUiAssetPublisher>.Instance);

            await sut.PublishAllAsync();

            var manifestPath = Path.Combine(webRoot, "plugin-assets", ".build", "ui-assets.manifest.json");
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
            var pluginIds = document.RootElement.GetProperty("entries").EnumerateArray()
                .Select(x => x.GetProperty("pluginId").GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.DoesNotContain("template-alpha", pluginIds);
            Assert.False(Directory.Exists(Path.Combine(webRoot, "plugin-assets", "template-alpha")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PublishAllAsync_FlattensSrcWrapper_ManifestContainsOnlyFinalPaths()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"callora-plugin-assets-{Guid.NewGuid():N}");
        var pluginDirectory = Path.Combine(tempRoot, "custom", "plugins");
        var webRoot = Path.Combine(tempRoot, "wwwroot");
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(webRoot);

        try
        {
            CreatePlugin(
                pluginDirectory,
                pluginFolderName: "TemplateAlpha",
                pluginId: "template-alpha",
                createAdmin: true,
                createWorkspace: true,
                createWorkspaceTemplate: false);

            var sut = new PluginUiAssetPublisher(
                new InMemoryPluginInstallationRepository(),
                new TestWebHostEnvironment { WebRootPath = webRoot, ContentRootPath = tempRoot },
                new CalloraHostingOptions { PluginDirectory = pluginDirectory },
                NullLogger<PluginUiAssetPublisher>.Instance);

            await sut.PublishAllAsync();

            var manifestPath = Path.Combine(webRoot, "plugin-assets", ".build", "ui-assets.manifest.json");
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
            var entryPaths = document.RootElement.GetProperty("entries").EnumerateArray()
                .Select(x => x.GetProperty("entryPath").GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToArray();

            Assert.NotEmpty(entryPaths);
            Assert.All(entryPaths, path => Assert.DoesNotContain("/src/", path));
            Assert.Contains("template-alpha/app/admin/main.js", entryPaths);
            Assert.True(File.Exists(Path.Combine(webRoot, "plugin-assets", "template-alpha", "app", "admin", "main.js")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PublishAllAsync_TypeScriptOnlyAdminEntry_ProducesNoManifestEntry()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"callora-plugin-assets-{Guid.NewGuid():N}");
        var pluginDirectory = Path.Combine(tempRoot, "custom", "plugins");
        var webRoot = Path.Combine(tempRoot, "wwwroot");
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(webRoot);

        try
        {
            // Unbuilt plugin: only a TypeScript source entry, no built .js.
            CreatePlugin(
                pluginDirectory,
                pluginFolderName: "TemplateTs",
                pluginId: "template-ts",
                createAdmin: true,
                createWorkspace: false,
                createWorkspaceTemplate: false,
                adminEntryFileName: "main.ts");

            var logger = new CapturingLogger<PluginUiAssetPublisher>();
            var sut = new PluginUiAssetPublisher(
                new InMemoryPluginInstallationRepository(),
                new TestWebHostEnvironment { WebRootPath = webRoot, ContentRootPath = tempRoot },
                new CalloraHostingOptions { PluginDirectory = pluginDirectory },
                logger);

            await sut.PublishAllAsync();

            var manifestPath = Path.Combine(webRoot, "plugin-assets", ".build", "ui-assets.manifest.json");
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
            var entryPaths = document.RootElement.GetProperty("entries").EnumerateArray()
                .Select(x => x.GetProperty("entryPath").GetString())
                .ToArray();

            // The .ts source is not a loadable entry, so nothing lands in the manifest.
            Assert.DoesNotContain(entryPaths, path => path is not null && path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(entryPaths);

            // The unbuilt TypeScript plugin is diagnosed, not silently dropped.
            Assert.Contains(
                logger.Entries,
                entry => entry.Level == LogLevel.Warning && entry.Message.Contains("TypeScript entry", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PublishAllAsync_LeavesNoStagingOrBackupDirectories()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"callora-plugin-assets-{Guid.NewGuid():N}");
        var pluginDirectory = Path.Combine(tempRoot, "custom", "plugins");
        var webRoot = Path.Combine(tempRoot, "wwwroot");
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(webRoot);

        try
        {
            CreatePlugin(
                pluginDirectory,
                pluginFolderName: "TemplateAlpha",
                pluginId: "template-alpha",
                createAdmin: true,
                createWorkspace: false,
                createWorkspaceTemplate: false);

            var sut = new PluginUiAssetPublisher(
                new InMemoryPluginInstallationRepository(),
                new TestWebHostEnvironment { WebRootPath = webRoot, ContentRootPath = tempRoot },
                new CalloraHostingOptions { PluginDirectory = pluginDirectory },
                NullLogger<PluginUiAssetPublisher>.Instance);

            await sut.PublishAllAsync();

            // The staging and backup directories are transient — cleaned up after the swap.
            Assert.False(Directory.Exists(Path.Combine(webRoot, ".plugin-assets-staging")));
            Assert.False(Directory.Exists(Path.Combine(webRoot, ".plugin-assets-old")));
            Assert.True(Directory.Exists(Path.Combine(webRoot, "plugin-assets", "template-alpha")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PublishAllAsync_Republish_ReplacesStalePluginAssets()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"callora-plugin-assets-{Guid.NewGuid():N}");
        var pluginDirectory = Path.Combine(tempRoot, "custom", "plugins");
        var webRoot = Path.Combine(tempRoot, "wwwroot");
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(webRoot);

        try
        {
            var pluginRoot = CreatePlugin(
                pluginDirectory,
                pluginFolderName: "TemplateAlpha",
                pluginId: "template-alpha",
                createAdmin: true,
                createWorkspace: false,
                createWorkspaceTemplate: false);

            var sut = new PluginUiAssetPublisher(
                new InMemoryPluginInstallationRepository(),
                new TestWebHostEnvironment { WebRootPath = webRoot, ContentRootPath = tempRoot },
                new CalloraHostingOptions { PluginDirectory = pluginDirectory },
                NullLogger<PluginUiAssetPublisher>.Instance);

            await sut.PublishAllAsync();
            Assert.True(Directory.Exists(Path.Combine(webRoot, "plugin-assets", "template-alpha")));

            // The plugin is gone on the next publish; the swap must replace, not merge.
            Directory.Delete(pluginRoot, recursive: true);
            await sut.PublishAllAsync();

            Assert.False(Directory.Exists(Path.Combine(webRoot, "plugin-assets", "template-alpha")));
            var manifestPath = Path.Combine(webRoot, "plugin-assets", ".build", "ui-assets.manifest.json");
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
            Assert.Empty(document.RootElement.GetProperty("entries").EnumerateArray().ToArray());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PublishAllAsync_EntryCarriesContentDerivedHash_ForCacheBusting()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"callora-plugin-assets-{Guid.NewGuid():N}");
        var pluginDirectory = Path.Combine(tempRoot, "custom", "plugins");
        var webRoot = Path.Combine(tempRoot, "wwwroot");
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(webRoot);

        try
        {
            var pluginRoot = CreatePlugin(
                pluginDirectory,
                pluginFolderName: "TemplateAlpha",
                pluginId: "template-alpha",
                createAdmin: false,
                createWorkspace: true,
                createWorkspaceTemplate: false);
            var workspaceEntry = Path.Combine(pluginRoot, "src", "Resources", "app", "workspace", "src", "main.js");

            var environment = new TestWebHostEnvironment { WebRootPath = webRoot, ContentRootPath = tempRoot };
            var hostingOptions = new CalloraHostingOptions { PluginDirectory = pluginDirectory };
            var manifestPath = Path.Combine(webRoot, "plugin-assets", ".build", "ui-assets.manifest.json");

            PluginUiAssetPublisher NewPublisher() => new(
                new InMemoryPluginInstallationRepository(),
                environment,
                hostingOptions,
                NullLogger<PluginUiAssetPublisher>.Instance);

            await NewPublisher().PublishAllAsync();
            var firstHash = ReadWorkspaceEntryHash(await File.ReadAllTextAsync(manifestPath));

            // A well-formed short hash (8 bytes → 16 lowercase-hex chars), not null/empty.
            Assert.NotNull(firstHash);
            Assert.Equal(16, firstHash!.Length);
            Assert.Matches("^[0-9a-f]{16}$", firstHash);

            // Change the bundle content and republish: the hash must change, so the URL
            // (main.js?v=<hash>) changes and a cached copy is never served stale.
            await File.WriteAllTextAsync(workspaceEntry, "console.log('workspace v2 changed');");
            await NewPublisher().PublishAllAsync();
            var secondHash = ReadWorkspaceEntryHash(await File.ReadAllTextAsync(manifestPath));

            Assert.NotEqual(firstHash, secondHash);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PublishAllAsync_PackagedResourcesRoot_IsPublished()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"callora-plugin-assets-{Guid.NewGuid():N}");
        var pluginDirectory = Path.Combine(tempRoot, "custom", "plugins");
        var pluginRoot = Path.Combine(pluginDirectory, "videoconference");
        var adminDirectory = Path.Combine(pluginRoot, "Resources", "public", "admin");
        var webRoot = Path.Combine(tempRoot, "wwwroot");
        Directory.CreateDirectory(adminDirectory);
        Directory.CreateDirectory(webRoot);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(pluginRoot, "registry.json"),
                """{ "pluginId": "videoconference", "assemblyFileName": "Callora.Plugin.VideoConference.dll" }""");
            await File.WriteAllTextAsync(
                Path.Combine(adminDirectory, "main.js"),
                "console.log('videoconference admin');");
            var sut = new PluginUiAssetPublisher(
                new InMemoryPluginInstallationRepository(),
                new TestWebHostEnvironment { WebRootPath = webRoot, ContentRootPath = tempRoot },
                new CalloraHostingOptions { PluginDirectory = pluginDirectory },
                NullLogger<PluginUiAssetPublisher>.Instance);

            await sut.PublishAllAsync();

            var manifestPath = Path.Combine(webRoot, "plugin-assets", ".build", "ui-assets.manifest.json");
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
            var entry = Assert.Single(document.RootElement.GetProperty("entries").EnumerateArray());
            Assert.Equal("videoconference", entry.GetProperty("pluginId").GetString());
            Assert.Equal("videoconference/app/admin/main.js", entry.GetProperty("entryPath").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string? ReadWorkspaceEntryHash(string manifestJson)
    {
        using var document = JsonDocument.Parse(manifestJson);
        var entry = document.RootElement.GetProperty("entries").EnumerateArray()
            .Single(x => x.GetProperty("surface").GetString() == "workspace");
        return entry.TryGetProperty("contentHash", out var hash) ? hash.GetString() : null;
    }

    private static string CreatePlugin(
        string pluginDirectory,
        string pluginFolderName,
        string pluginId,
        bool createAdmin,
        bool createWorkspace,
        bool createWorkspaceTemplate,
        string adminEntryFileName = "main.js")
    {
        var pluginRoot = Path.Combine(pluginDirectory, pluginFolderName);
        Directory.CreateDirectory(pluginRoot);

        var registryPath = Path.Combine(pluginRoot, "registry.json");
        File.WriteAllText(
            registryPath,
            $$"""
              {
                "pluginId": "{{pluginId}}",
                "assemblyFileName": "Callora.Plugins.{{pluginFolderName}}.dll"
              }
              """);

        if (createAdmin)
        {
            var adminDirectory = Path.Combine(pluginRoot, "src", "Resources", "app", "admin", "src");
            Directory.CreateDirectory(adminDirectory);
            File.WriteAllText(Path.Combine(adminDirectory, adminEntryFileName), "console.log('admin');");
        }

        if (createWorkspace)
        {
            var workspaceDirectory = Path.Combine(pluginRoot, "src", "Resources", "app", "workspace", "src");
            Directory.CreateDirectory(workspaceDirectory);
            File.WriteAllText(Path.Combine(workspaceDirectory, "main.js"), "console.log('workspace');");
        }

        if (createWorkspaceTemplate)
        {
            var templateDirectory = Path.Combine(pluginRoot, "src", "Resources", "views", "workspace");
            Directory.CreateDirectory(templateDirectory);
            File.WriteAllText(Path.Combine(templateDirectory, "base.html"), "<div>workspace template</div>");
        }

        return pluginRoot;
    }
}
