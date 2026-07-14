using System.Text.Json;
using Callora.Host.Backend.Domain.Plugins;
using Callora.Host.Backend.Infrastructure.Plugins;
using Callora.Host.Backend.Tests.Support;
using Callora.Hosting.Application.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Host.Backend.Tests.Infrastructure.Plugins;

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

    private static string CreatePlugin(
        string pluginDirectory,
        string pluginFolderName,
        string pluginId,
        bool createAdmin,
        bool createWorkspace,
        bool createWorkspaceTemplate)
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
            File.WriteAllText(Path.Combine(adminDirectory, "main.js"), "console.log('admin');");
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
