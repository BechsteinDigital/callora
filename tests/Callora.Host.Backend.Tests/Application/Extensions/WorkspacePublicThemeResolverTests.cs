using Callora.Host.Backend.Application.Extensions;
using Callora.Host.Backend.Tests.Support;
using Xunit;

namespace Callora.Host.Backend.Tests.Application.Extensions;

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

        var resolver = new WorkspacePublicThemeResolver(workspaceStore, settingsStore);
        var theme = await resolver.ResolveAsync("workspace-a");

        Assert.NotNull(theme);
        Assert.Equal("#336699", theme.ValuesByKey["primary.color"]);
        Assert.False(theme.ValuesByKey.ContainsKey("branding.apiToken"));
    }

    [Fact]
    public async Task Resolve_ReturnsNull_ForInactiveWorkspace()
    {
        var (workspaceStore, settingsStore) = await CreateStoresWithWorkspaceAsync(isActive: false);

        var resolver = new WorkspacePublicThemeResolver(workspaceStore, settingsStore);

        Assert.Null(await resolver.ResolveAsync("workspace-a"));
    }

    [Fact]
    public async Task Resolve_ReturnsNull_WhenTenantIsInactive()
    {
        var (workspaceStore, settingsStore) = await CreateStoresWithWorkspaceAsync(isActive: true);
        workspaceStore.SetTenantActive("tenant-a", isActive: false);

        var resolver = new WorkspacePublicThemeResolver(workspaceStore, settingsStore);

        Assert.Null(await resolver.ResolveAsync("workspace-a"));
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
