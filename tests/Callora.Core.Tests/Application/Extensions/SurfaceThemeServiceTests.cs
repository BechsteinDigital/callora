using Callora.Core.Application.Extensions;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Extensions;
using Callora.Core.Tests.Support;

namespace Callora.Core.Tests.Application.Extensions;

/// <summary>
/// Per-surface theming: a surface may run its workspace's theme with its own
/// values, or a different theme entirely — and the values cascade
/// default → workspace → surface, except across a theme change.
/// </summary>
public sealed class SurfaceThemeServiceTests
{
    private const string WorkspaceKey = "workspace-a";
    private const string SurfaceKey = "shop";
    private const string WorkspaceTheme = "theme-alpha";
    private const string SurfaceTheme = "theme-beta";
    private const string Version = "1.0.0";

    [Fact]
    public async Task Assignment_WithoutOwnTheme_FollowsTheWorkspace()
    {
        var fixture = await CreateAsync();

        var result = await fixture.Service.GetAssignmentAsync(WorkspaceKey, SurfaceKey);

        Assert.Equal(SurfaceThemeStatus.Ok, result.Status);
        Assert.True(result.Assignment!.InheritedFromWorkspace);
        Assert.Equal(WorkspaceTheme, result.Assignment.ThemePluginId);
    }

    [Fact]
    public async Task Assign_PinsTheSurfaceToItsOwnTheme()
    {
        var fixture = await CreateAsync();

        var result = await fixture.Service.AssignAsync(WorkspaceKey, SurfaceKey, SurfaceTheme, Version);

        Assert.Equal(SurfaceThemeStatus.Ok, result.Status);
        Assert.False(result.Assignment!.InheritedFromWorkspace);
        Assert.Equal(SurfaceTheme, result.Assignment.ThemePluginId);
    }

    [Fact]
    public async Task Assign_RejectsAThemeWithoutAnActiveDefinition()
    {
        var fixture = await CreateAsync();

        var result = await fixture.Service.AssignAsync(WorkspaceKey, SurfaceKey, "theme-unknown", Version);

        Assert.Equal(SurfaceThemeStatus.ThemeNotFound, result.Status);
    }

    [Fact]
    public async Task Assign_KeepsEveryOtherSurfaceFieldIntact()
    {
        // The surface upsert is a full replace — a theme change must not clear
        // the routing or template fields.
        var fixture = await CreateAsync();

        await fixture.Service.AssignAsync(WorkspaceKey, SurfaceKey, SurfaceTheme, Version);

        var surface = await fixture.Surfaces.GetAsync(WorkspaceKey, SurfaceKey);
        Assert.Equal("/shop", surface!.PublicPathPrefix);
        Assert.Equal("shop.example.test", surface.PublicHost);
        Assert.Equal("template-x", surface.TemplatePluginId);
        Assert.True(surface.IsActive);
    }

    [Fact]
    public async Task Clear_ReturnsTheSurfaceToTheWorkspaceTheme()
    {
        var fixture = await CreateAsync();
        await fixture.Service.AssignAsync(WorkspaceKey, SurfaceKey, SurfaceTheme, Version);

        var result = await fixture.Service.ClearAsync(WorkspaceKey, SurfaceKey);

        Assert.Equal(SurfaceThemeStatus.Ok, result.Status);
        Assert.True(result.Assignment!.InheritedFromWorkspace);
        Assert.Equal(WorkspaceTheme, result.Assignment.ThemePluginId);
    }

    [Fact]
    public async Task Clear_DropsTheValuesOfTheDetachedTheme()
    {
        var fixture = await CreateAsync();
        await fixture.Service.AssignAsync(WorkspaceKey, SurfaceKey, SurfaceTheme, Version);
        await fixture.Service.ReplaceSettingsAsync(WorkspaceKey, SurfaceKey, Values(("primary.color", "\"#111111\"")));

        await fixture.Service.ClearAsync(WorkspaceKey, SurfaceKey);

        // Otherwise they would silently reappear when that theme is assigned again.
        var orphaned = await fixture.Settings.ListValuesAsync(WorkspaceKey, SurfaceKey, SurfaceTheme);
        Assert.Empty(orphaned);
    }

    [Fact]
    public async Task Settings_OnTheSharedTheme_InheritTheWorkspaceValues()
    {
        var fixture = await CreateAsync();
        await fixture.Settings.ReplaceValuesAsync(
            WorkspaceKey,
            surfaceKey: null,
            WorkspaceTheme,
            Values(("primary.color", "\"#336699\"")));

        var result = await fixture.Service.GetSettingsAsync(WorkspaceKey, SurfaceKey);

        Assert.True(result.Settings!.InheritsWorkspaceValues);
        Assert.Equal("\"#336699\"", result.Settings.InheritedValuesByKey["primary.color"]);
        Assert.Empty(result.Settings.OwnValuesByKey);
    }

    [Fact]
    public async Task Settings_OnADifferentTheme_DoNotInheritTheWorkspaceValues()
    {
        // Those values were entered for another theme's setting keys.
        var fixture = await CreateAsync();
        await fixture.Settings.ReplaceValuesAsync(
            WorkspaceKey,
            surfaceKey: null,
            WorkspaceTheme,
            Values(("primary.color", "\"#336699\"")));
        await fixture.Service.AssignAsync(WorkspaceKey, SurfaceKey, SurfaceTheme, Version);

        var result = await fixture.Service.GetSettingsAsync(WorkspaceKey, SurfaceKey);

        Assert.False(result.Settings!.InheritsWorkspaceValues);
        Assert.Empty(result.Settings.InheritedValuesByKey);
    }

    [Fact]
    public async Task ReplaceSettings_StoresOnlyOnTheSurfaceLevel()
    {
        var fixture = await CreateAsync();

        await fixture.Service.ReplaceSettingsAsync(WorkspaceKey, SurfaceKey, Values(("primary.color", "\"#ff0000\"")));

        var surfaceValues = await fixture.Settings.ListValuesAsync(WorkspaceKey, SurfaceKey, WorkspaceTheme);
        var workspaceValues = await fixture.Settings.ListValuesAsync(WorkspaceKey, surfaceKey: null, WorkspaceTheme);
        Assert.Single(surfaceValues);
        Assert.Empty(workspaceValues);
    }

    [Fact]
    public async Task ReplaceSettings_IgnoresKeysTheThemeDoesNotDeclare()
    {
        var fixture = await CreateAsync();

        await fixture.Service.ReplaceSettingsAsync(
            WorkspaceKey,
            SurfaceKey,
            Values(("primary.color", "\"#ff0000\""), ("unknown.key", "\"x\"")));

        var stored = await fixture.Settings.ListValuesAsync(WorkspaceKey, SurfaceKey, WorkspaceTheme);
        Assert.Single(stored);
        Assert.Equal("primary.color", stored[0].SettingKey);
    }

    [Fact]
    public async Task ReplaceSettings_WithoutAnyTheme_IsRefused()
    {
        var fixture = await CreateAsync(assignWorkspaceTheme: false);

        var result = await fixture.Service.ReplaceSettingsAsync(
            WorkspaceKey,
            SurfaceKey,
            Values(("primary.color", "\"#ff0000\"")));

        Assert.Equal(SurfaceThemeStatus.NoThemeAssigned, result.Status);
    }

    [Fact]
    public async Task UnknownSurface_IsReportedAsSuch()
    {
        var fixture = await CreateAsync();

        var result = await fixture.Service.GetAssignmentAsync(WorkspaceKey, "does-not-exist");

        Assert.Equal(SurfaceThemeStatus.SurfaceNotFound, result.Status);
    }

    [Fact]
    public async Task UnknownWorkspace_IsReportedAsSuch()
    {
        var fixture = await CreateAsync();

        var result = await fixture.Service.GetAssignmentAsync("no-such-workspace", SurfaceKey);

        Assert.Equal(SurfaceThemeStatus.WorkspaceNotFound, result.Status);
    }

    private static Dictionary<string, string?> Values(params (string Key, string Json)[] values) =>
        values.ToDictionary(pair => pair.Key, pair => (string?)pair.Json, StringComparer.OrdinalIgnoreCase);

    private static async Task<SurfaceThemeFixture> CreateAsync(bool assignWorkspaceTheme = true)
    {
        var workspaces = new InMemoryWorkspaceManagementStore();
        workspaces.AddTenant("tenant-a");
        _ = await workspaces.UpsertAsync("tenant-a", WorkspaceKey, "Workspace A", "shop", true);

        var surfaces = new InMemoryWorkspaceSurfaceStore();
        _ = await surfaces.UpsertAsync(
            WorkspaceKey,
            new WorkspaceSurfaceInput(
                SurfaceKey,
                "Shop",
                "spa",
                PublicBaseUrl: null,
                PublicHost: "shop.example.test",
                PublicPathPrefix: "/shop",
                AccessMode: SurfaceAccessMode.Public,
                Locale: "de",
                TemplatePluginId: "template-x",
                TemplateVersion: "2.0.0",
                ThemePluginId: null,
                ThemeVersion: null,
                IsActive: true));

        var templates = new InMemoryWorkspaceTemplateRegistryStore();
        foreach (var pluginId in new[] { WorkspaceTheme, SurfaceTheme })
        {
            _ = await templates.UpsertDefinitionAsync(
                $"workspace.{pluginId}",
                "workspace",
                pluginId,
                Version,
                pluginId,
                $"themes/{pluginId}.html",
                parentTemplateKey: null,
                scope: "workspace",
                isActive: true,
                priority: 100);
        }

        var settings = new InMemoryWorkspaceThemeSettingsStore();
        foreach (var pluginId in new[] { WorkspaceTheme, SurfaceTheme })
        {
            _ = await settings.ReplaceDefinitionsForPluginAsync(
                pluginId,
                Version,
                [new("primary.color", "Primärfarbe", "color", null, "\"#000000\"", false, 0, null, null, true)]);
        }

        if (assignWorkspaceTheme)
        {
            _ = await workspaces.UpsertThemeAssignmentAsync(WorkspaceKey, WorkspaceTheme, Version, "tester");
        }

        return new SurfaceThemeFixture(
            new SurfaceThemeService(workspaces, surfaces, templates, settings),
            surfaces,
            settings);
    }

    private sealed record SurfaceThemeFixture(
        SurfaceThemeService Service,
        InMemoryWorkspaceSurfaceStore Surfaces,
        InMemoryWorkspaceThemeSettingsStore Settings);
}
