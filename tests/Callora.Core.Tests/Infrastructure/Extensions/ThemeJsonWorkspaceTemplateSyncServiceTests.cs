using Callora.Core.Infrastructure.Extensions;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Core.Tests.Infrastructure.Extensions;

public sealed class ThemeJsonWorkspaceTemplateSyncServiceTests
{
    [Fact]
    public async Task SyncFromAssemblyAsync_ReplacesExistingPluginDefinitionsFromThemeJson()
    {
        var store = new InMemoryWorkspaceTemplateRegistryStore();
        var settingsStore = new InMemoryWorkspaceThemeSettingsStore();
        var sut = new ThemeJsonWorkspaceTemplateSyncService(store, settingsStore, NullLogger<ThemeJsonWorkspaceTemplateSyncService>.Instance);
        var pluginId = "theme-acme";
        var tempDir = Path.Combine(Path.GetTempPath(), $"callora-theme-sync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var assemblyPath = Path.Combine(tempDir, "Theme.Acme.dll");
            await File.WriteAllBytesAsync(assemblyPath, []);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "theme.json"), """
            {
              "surface": "surface",
              "definitions": [
                {
                  "templateKey": "workspace.layout",
                  "displayName": "Layout",
                  "templatePath": "/themes/layout.json",
                  "scope": "workspace",
                  "isActive": true,
                  "priority": 200
                },
                {
                  "key": "workspace.login",
                  "name": "Login",
                  "path": "/themes/login.json"
                }
              ],
              "config": {
                "fields": {
                  "brandColor": {
                    "label": "Brand Color",
                    "type": "color",
                    "value": "#ffffff"
                  },
                  "headline": {
                    "label": "Headline",
                    "type": "text",
                    "value": "Callora"
                  }
                }
              }
            }
            """);

            await sut.SyncFromAssemblyAsync(pluginId, "1.0.0", assemblyPath);

            var first = await store.ListDefinitionsAsync(surface: "surface", isActive: null);
            var firstSettings = await settingsStore.ListDefinitionsAsync(pluginId, "1.0.0");
            Assert.Equal(2, first.Count);
            Assert.Equal(2, firstSettings.Count);
            Assert.All(first, x =>
            {
                Assert.Equal(pluginId, x.PluginId);
                Assert.Equal("1.0.0", x.Version);
            });

            await File.WriteAllTextAsync(Path.Combine(tempDir, "theme.json"), """
            {
              "templates": [
                {
                  "id": "workspace.settings",
                  "label": "Settings",
                  "template": "/themes/settings.json",
                  "active": true,
                  "order": 50
                }
              ],
              "config": {
                "fields": {
                  "headline": {
                    "label": "Headline",
                    "type": "text",
                    "value": "Updated"
                  }
                }
              }
            }
            """);

            await sut.SyncFromAssemblyAsync(pluginId, "1.1.0", assemblyPath);

            var second = await store.ListDefinitionsAsync(surface: "surface", isActive: null);
            var firstVersionSettings = await settingsStore.ListDefinitionsAsync(pluginId, "1.0.0");
            var secondSettings = await settingsStore.ListDefinitionsAsync(pluginId, "1.1.0");
            Assert.Equal(3, second.Count);
            Assert.Equal(2, firstVersionSettings.Count);
            Assert.Single(secondSettings);
            var latestDefinition = Assert.Single(second, x => string.Equals(x.Version, "1.1.0", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("workspace.settings", latestDefinition.TemplateKey);
            Assert.Equal("1.1.0", latestDefinition.Version);
            Assert.Equal("headline", secondSettings[0].SettingKey);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SyncFromAssemblyAsync_NoThemeJson_DoesNotMutateExistingDefinitions()
    {
        var store = new InMemoryWorkspaceTemplateRegistryStore();
        var settingsStore = new InMemoryWorkspaceThemeSettingsStore();
        var sut = new ThemeJsonWorkspaceTemplateSyncService(store, settingsStore, NullLogger<ThemeJsonWorkspaceTemplateSyncService>.Instance);
        var pluginId = "theme-acme";

        await store.UpsertDefinitionAsync(
            templateKey: "workspace.layout",
            surface: "surface",
            pluginId: pluginId,
            version: "1.0.0",
            displayName: "Layout",
            templatePath: "/themes/layout.json",
            parentTemplateKey: null,
            scope: "workspace",
            isActive: true,
            priority: 100);

        var tempDir = Path.Combine(Path.GetTempPath(), $"callora-theme-sync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var assemblyPath = Path.Combine(tempDir, "Theme.Acme.dll");
            await File.WriteAllBytesAsync(assemblyPath, []);

            await sut.SyncFromAssemblyAsync(pluginId, "1.2.0", assemblyPath);
            var definitions = await store.ListDefinitionsAsync();
            Assert.Single(definitions);
            Assert.Equal("workspace.layout", definitions[0].TemplateKey);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ClearPluginDefinitionsAsync_RemovesPluginRows()
    {
        var store = new InMemoryWorkspaceTemplateRegistryStore();
        var settingsStore = new InMemoryWorkspaceThemeSettingsStore();
        var sut = new ThemeJsonWorkspaceTemplateSyncService(store, settingsStore, NullLogger<ThemeJsonWorkspaceTemplateSyncService>.Instance);

        await store.UpsertDefinitionAsync(
            templateKey: "workspace.layout",
            surface: "surface",
            pluginId: "theme-a",
            version: "1.0.0",
            displayName: "Layout",
            templatePath: "/themes/layout.json",
            parentTemplateKey: null,
            scope: "workspace",
            isActive: true,
            priority: 100);
        await store.UpsertDefinitionAsync(
            templateKey: "workspace.layout",
            surface: "surface",
            pluginId: "theme-b",
            version: "1.0.0",
            displayName: "Layout",
            templatePath: "/themes/layout.json",
            parentTemplateKey: null,
            scope: "workspace",
            isActive: true,
            priority: 100);

        await sut.ClearPluginDefinitionsAsync("theme-a");

        var definitions = await store.ListDefinitionsAsync();
        var settings = await settingsStore.ListDefinitionsAsync("theme-a", "1.0.0");
        Assert.Single(definitions);
        Assert.Equal("theme-b", definitions[0].PluginId);
        Assert.Empty(settings);
    }
}
