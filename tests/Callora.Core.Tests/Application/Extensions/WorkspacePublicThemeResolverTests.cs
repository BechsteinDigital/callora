using Callora.Core.Application.Extensions;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Application.Extensions;

public sealed class WorkspacePublicThemeResolverTests
{
    private const string ThemePluginId = "glass-theme";
    private const string ThemeVersion = "1.0.0";

    [Fact]
    public async Task Resolve_ExcludesSecretSettings_FromPublicTheme()
    {
        var (workspaceStore, settingsStore) = await CreateStoresWithWorkspaceAsync(isActive: true);
        await settingsStore.ReplaceDefinitionsForPluginAsync(ThemePluginId, ThemeVersion,
        [
            new("primary.color", "Primärfarbe", "color", null, "\"#336699\"", false, 0, null, null, true),
            new("branding.apiToken", "API-Token", "secret", null, "\"top-secret\"", false, 1, null, null, true)
        ]);

        var resolver = new WorkspacePublicThemeResolver(workspaceStore, new InMemoryWorkspaceSurfaceStore(), settingsStore);
        var theme = await resolver.ResolveAsync("workspace-a");

        Assert.NotNull(theme);
        Assert.Equal("#336699", theme.ValuesByKey["primary.color"]);
        Assert.False(theme.ValuesByKey.ContainsKey("branding.apiToken"));
    }

    [Fact]
    public async Task Resolve_ReturnsNull_ForInactiveWorkspace()
    {
        var (workspaceStore, settingsStore) = await CreateStoresWithWorkspaceAsync(isActive: false);

        var resolver = new WorkspacePublicThemeResolver(workspaceStore, new InMemoryWorkspaceSurfaceStore(), settingsStore);

        Assert.Null(await resolver.ResolveAsync("workspace-a"));
    }

    [Fact]
    public async Task Resolve_ReturnsNull_WhenTenantIsInactive()
    {
        var (workspaceStore, settingsStore) = await CreateStoresWithWorkspaceAsync(isActive: true);
        workspaceStore.SetTenantActive("tenant-a", isActive: false);

        var resolver = new WorkspacePublicThemeResolver(workspaceStore, new InMemoryWorkspaceSurfaceStore(), settingsStore);

        Assert.Null(await resolver.ResolveAsync("workspace-a"));
    }


    [Fact]
    public async Task ResolveForSurface_PrefersTheSurfaceValueOverTheWorkspaceValue()
    {
        var (workspaceStore, settingsStore) = await CreateStoresWithWorkspaceAsync(isActive: true);
        var surfaceStore = await CreateSurfaceAsync(themePluginId: null);
        await settingsStore.ReplaceDefinitionsForPluginAsync(ThemePluginId, ThemeVersion,
        [
            new("primary.color", "Primärfarbe", "color", null, "\"#000000\"", false, 0, null, null, true),
            new("logo.text", "Logo", "text", null, "\"Default\"", false, 1, null, null, true)
        ]);
        await settingsStore.ReplaceValuesAsync("workspace-a", null, ThemePluginId,
            new Dictionary<string, string?> { ["primary.color"] = "\"#336699\"", ["logo.text"] = "\"Workspace\"" });
        await settingsStore.ReplaceValuesAsync("workspace-a", "shop", ThemePluginId,
            new Dictionary<string, string?> { ["primary.color"] = "\"#ff0000\"" });

        var resolver = new WorkspacePublicThemeResolver(workspaceStore, surfaceStore, settingsStore);
        var theme = await resolver.ResolveForSurfaceAsync("workspace-a", "shop");

        Assert.NotNull(theme);
        Assert.Equal("#ff0000", theme.ValuesByKey["primary.color"]);
        // Untouched keys still fall through to the workspace value.
        Assert.Equal("Workspace", theme.ValuesByKey["logo.text"]);
    }

    [Fact]
    public async Task ResolveForSurface_UsesTheSurfaceTheme_AndDropsTheWorkspaceValues()
    {
        // The workspace values belong to another theme's setting keys — carrying
        // them over would render one theme with another's configuration.
        var (workspaceStore, settingsStore) = await CreateStoresWithWorkspaceAsync(isActive: true);
        var surfaceStore = await CreateSurfaceAsync(themePluginId: "other-theme");
        await settingsStore.ReplaceDefinitionsForPluginAsync(ThemePluginId, ThemeVersion,
            [new("primary.color", "Primärfarbe", "color", null, "\"#000000\"", false, 0, null, null, true)]);
        await settingsStore.ReplaceDefinitionsForPluginAsync("other-theme", ThemeVersion,
            [new("primary.color", "Primärfarbe", "color", null, "\"#123456\"", false, 0, null, null, true)]);
        await settingsStore.ReplaceValuesAsync("workspace-a", null, ThemePluginId,
            new Dictionary<string, string?> { ["primary.color"] = "\"#336699\"" });

        var resolver = new WorkspacePublicThemeResolver(workspaceStore, surfaceStore, settingsStore);
        var theme = await resolver.ResolveForSurfaceAsync("workspace-a", "shop");

        Assert.NotNull(theme);
        Assert.Equal("other-theme", theme.ThemePluginId);
        Assert.Equal("#123456", theme.ValuesByKey["primary.color"]);
    }

    [Fact]
    public async Task ResolveForSurface_FallsBackToTheWorkspace_ForAnUnknownSurface()
    {
        var (workspaceStore, settingsStore) = await CreateStoresWithWorkspaceAsync(isActive: true);
        await settingsStore.ReplaceDefinitionsForPluginAsync(ThemePluginId, ThemeVersion,
            [new("primary.color", "Primärfarbe", "color", null, "\"#000000\"", false, 0, null, null, true)]);

        var resolver = new WorkspacePublicThemeResolver(workspaceStore, new InMemoryWorkspaceSurfaceStore(), settingsStore);
        var theme = await resolver.ResolveForSurfaceAsync("workspace-a", "does-not-exist");

        Assert.NotNull(theme);
        Assert.Equal(ThemePluginId, theme.ThemePluginId);
    }

    private static async Task<InMemoryWorkspaceSurfaceStore> CreateSurfaceAsync(string? themePluginId)
    {
        var surfaceStore = new InMemoryWorkspaceSurfaceStore();
        _ = await surfaceStore.UpsertAsync(
            "workspace-a",
            new WorkspaceSurfaceInput(
                "shop",
                "Shop",
                "spa",
                PublicBaseUrl: null,
                PublicHost: null,
                PublicPathPrefix: "/shop",
                AccessMode: SurfaceAccessMode.Public,
                Locale: "de",
                TemplatePluginId: null,
                TemplateVersion: null,
                ThemePluginId: themePluginId,
                ThemeVersion: themePluginId is null ? null : ThemeVersion,
                IsActive: true));
        return surfaceStore;
    }

    private static async Task<(InMemoryWorkspaceManagementStore WorkspaceStore, InMemoryWorkspaceThemeSettingsStore SettingsStore)>
        CreateStoresWithWorkspaceAsync(bool isActive)
    {
        var workspaceStore = new InMemoryWorkspaceManagementStore();
        workspaceStore.AddTenant("tenant-a");
        _ = await workspaceStore.UpsertAsync(
            tenantKey: "tenant-a",
            workspaceKey: "workspace-a",
            displayName: "Workspace A",
            workspaceType: "voice",
            isActive: isActive);
        _ = await workspaceStore.UpsertThemeAssignmentAsync("workspace-a", ThemePluginId, ThemeVersion, assignedBy: null);

        return (workspaceStore, new InMemoryWorkspaceThemeSettingsStore());
    }
}
