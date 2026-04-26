using System.Net;
using System.Net.Http.Json;
using Callora.Host.Backend.Api;
using Callora.Host.Workspace.Api;
using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Abstractions.Extensions;
using Callora.Host.Backend.Application.Abstractions.Workspaces;
using Callora.Host.Backend.Application.Extensions;
using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Infrastructure.Extensions;
using Callora.Host.Backend.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Host.Backend.Tests.Api;

public sealed class WorkspaceTemplateEndpointsTests
{
    [Fact]
    public async Task WorkspaceThemeAssignment_ResolvesEffectiveThemeDefinitions()
    {
        await using var app = await CreateAppAsync();

        var adminClient = app.GetTestClient();
        adminClient.DefaultRequestHeaders.Add("X-Test-Permissions", "extension.update,extension.read");

        var upsertAlpha = await adminClient.PutAsJsonAsync(
            "/api/themes/definitions/workspace.dashboard/plugins/template-alpha/versions/1.0.0",
            new ThemeDefinitionUpsertApiRequest(
                DisplayName: "Dashboard Alpha",
                TemplatePath: "custom/plugins/TemplateAlpha/src/Resources/views/workspace/layouts/dashboard.html",
                ParentTemplateKey: null,
                Scope: "workspace",
                IsActive: true,
                Priority: 100,
                Surface: "workspace"));
        Assert.Equal(HttpStatusCode.OK, upsertAlpha.StatusCode);

        var upsertBeta = await adminClient.PutAsJsonAsync(
            "/api/themes/definitions/workspace.dashboard/plugins/template-beta/versions/1.1.0",
            new ThemeDefinitionUpsertApiRequest(
                DisplayName: "Dashboard Beta",
                TemplatePath: "custom/plugins/TemplateBeta/src/Resources/views/workspace/layouts/dashboard.html",
                ParentTemplateKey: null,
                Scope: "workspace",
                IsActive: true,
                Priority: 110,
                Surface: "workspace"));
        Assert.Equal(HttpStatusCode.OK, upsertBeta.StatusCode);

        var assignAlpha = await adminClient.PutAsJsonAsync(
            "/api/themes/workspaces/workspace-a",
            new WorkspaceThemeAssignmentUpsertApiRequest("template-alpha", "1.0.0", "workspace-admin"));
        Assert.Equal(HttpStatusCode.OK, assignAlpha.StatusCode);

        var effectiveAlpha = await adminClient
            .GetFromJsonAsync<WorkspaceTemplateEffectiveApiResponse[]>("/api/themes/workspaces/workspace-a/effective");
        Assert.NotNull(effectiveAlpha);
        Assert.Single(effectiveAlpha!);
        Assert.Equal("template-alpha", effectiveAlpha![0].PluginId);

        var assignBeta = await adminClient.PutAsJsonAsync(
            "/api/themes/workspaces/workspace-a",
            new WorkspaceThemeAssignmentUpsertApiRequest("template-beta", "1.1.0", "workspace-admin"));
        Assert.Equal(HttpStatusCode.OK, assignBeta.StatusCode);

        var effectiveBeta = await adminClient
            .GetFromJsonAsync<WorkspaceTemplateEffectiveApiResponse[]>("/api/themes/workspaces/workspace-a/effective");
        Assert.NotNull(effectiveBeta);
        Assert.Single(effectiveBeta!);
        Assert.Equal("template-beta", effectiveBeta![0].PluginId);
        Assert.Equal("workspace-assigned", effectiveBeta[0].Source);
    }

    [Fact]
    public async Task WorkspaceThemeAssignment_ResolvesInheritedParentDefinitions()
    {
        await using var app = await CreateAppAsync();

        var adminClient = app.GetTestClient();
        adminClient.DefaultRequestHeaders.Add("X-Test-Permissions", "extension.update,extension.read");

        var upsertBase = await adminClient.PutAsJsonAsync(
            "/api/themes/definitions/workspace.base/plugins/template-alpha/versions/1.0.0",
            new ThemeDefinitionUpsertApiRequest(
                DisplayName: "Workspace Base",
                TemplatePath: "custom/plugins/TemplateAlpha/src/Resources/views/workspace/base.html",
                ParentTemplateKey: null,
                Scope: "workspace",
                IsActive: true,
                Priority: 80,
                Surface: "workspace"));
        Assert.Equal(HttpStatusCode.OK, upsertBase.StatusCode);

        var upsertChild = await adminClient.PutAsJsonAsync(
            "/api/themes/definitions/workspace.dashboard/plugins/template-beta/versions/1.1.0",
            new ThemeDefinitionUpsertApiRequest(
                DisplayName: "Workspace Dashboard Beta",
                TemplatePath: "custom/plugins/TemplateBeta/src/Resources/views/workspace/layouts/dashboard.html",
                ParentTemplateKey: "workspace.base",
                Scope: "workspace",
                IsActive: true,
                Priority: 110,
                Surface: "workspace"));
        Assert.Equal(HttpStatusCode.OK, upsertChild.StatusCode);

        var assign = await adminClient.PutAsJsonAsync(
            "/api/themes/workspaces/workspace-a",
            new WorkspaceThemeAssignmentUpsertApiRequest("template-beta", "1.1.0", "workspace-admin"));
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        var effective = await adminClient
            .GetFromJsonAsync<WorkspaceTemplateEffectiveApiResponse[]>("/api/themes/workspaces/workspace-a/effective");
        Assert.NotNull(effective);
        Assert.Equal(2, effective!.Length);
        Assert.Equal("template-beta", effective[0].PluginId);
        Assert.Equal("workspace-assigned", effective[0].Source);
        Assert.Equal("template-alpha", effective[1].PluginId);
        Assert.Equal("workspace-inherited", effective[1].Source);
    }

    [Fact]
    public async Task WorkspaceEffectiveEndpoint_UsesClaimWorkspaceKey()
    {
        await using var app = await CreateAppAsync();

        var adminClient = app.GetTestClient();
        adminClient.DefaultRequestHeaders.Add("X-Test-Permissions", "extension.update,extension.read");

        _ = await adminClient.PutAsJsonAsync(
            "/api/themes/definitions/workspace.dashboard/plugins/template-alpha/versions/1.0.0",
            new ThemeDefinitionUpsertApiRequest(
                DisplayName: "Dashboard Alpha",
                TemplatePath: "custom/plugins/TemplateAlpha/src/Resources/views/workspace/layouts/dashboard.html",
                ParentTemplateKey: null,
                Scope: "workspace",
                IsActive: true,
                Priority: 100,
                Surface: "workspace"));

        _ = await adminClient.PutAsJsonAsync(
            "/api/themes/workspaces/workspace-a",
            new WorkspaceThemeAssignmentUpsertApiRequest("template-alpha", "1.0.0", "workspace-admin"));

        var workspaceClient = app.GetTestClient();
        workspaceClient.DefaultRequestHeaders.Add("X-Test-Permissions", "extension.read");
        workspaceClient.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "workspace-a");

        var effective = await workspaceClient
            .GetFromJsonAsync<WorkspaceTemplateEffectiveApiResponse[]>("/workspace/themes/effective");

        Assert.NotNull(effective);
        Assert.Single(effective!);
        Assert.Equal("workspace-a", effective![0].WorkspaceKey);
        Assert.Equal("template-alpha", effective[0].PluginId);
    }

    [Fact]
    public async Task WorkspaceThemeSettings_ReadAndWrite_WorksForAssignedTheme()
    {
        await using var app = await CreateAppAsync();

        var adminClient = app.GetTestClient();
        adminClient.DefaultRequestHeaders.Add("X-Test-Permissions", "extension.update,extension.read");

        var upsertTheme = await adminClient.PutAsJsonAsync(
            "/api/themes/definitions/workspace.dashboard/plugins/template-alpha/versions/1.0.0",
            new ThemeDefinitionUpsertApiRequest(
                DisplayName: "Dashboard Alpha",
                TemplatePath: "custom/plugins/TemplateAlpha/src/Resources/views/workspace/layouts/dashboard.html",
                ParentTemplateKey: null,
                Scope: "workspace",
                IsActive: true,
                Priority: 100,
                Surface: "workspace"));
        Assert.Equal(HttpStatusCode.OK, upsertTheme.StatusCode);

        var assign = await adminClient.PutAsJsonAsync(
            "/api/themes/workspaces/workspace-a",
            new WorkspaceThemeAssignmentUpsertApiRequest("template-alpha", "1.0.0", "workspace-admin"));
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        var settingsStore = app.Services.GetRequiredService<IWorkspaceThemeSettingsStore>();
        _ = await settingsStore.ReplaceDefinitionsForPluginAsync(
            "template-alpha",
            "1.0.0",
            [
                new WorkspaceThemeSettingDefinitionInput(
                    SettingKey: "brandColor",
                    Label: "Brand Color",
                    FieldType: "color",
                    Description: null,
                    DefaultValueJson: "\"#ffffff\"",
                    IsRequired: false,
                    SortOrder: 10,
                    GroupName: "Colors",
                    OptionsJson: null,
                    IsActive: true)
            ]);

        var before = await adminClient
            .GetFromJsonAsync<WorkspaceThemeSettingsApiResponse>("/api/themes/workspaces/workspace-a/settings");
        Assert.NotNull(before);
        Assert.True(before!.HasAssignedTheme);
        Assert.Single(before.Fields);
        Assert.Empty(before.ValuesByKey);

        var upsert = await adminClient.PutAsJsonAsync(
            "/api/themes/workspaces/workspace-a/settings",
            new
            {
                valuesByKey = new Dictionary<string, object?>
                {
                    ["brandColor"] = "#1f2937"
                }
            });
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);

        var after = await adminClient
            .GetFromJsonAsync<WorkspaceThemeSettingsApiResponse>("/api/themes/workspaces/workspace-a/settings");
        Assert.NotNull(after);
        Assert.True(after!.ValuesByKey.ContainsKey("brandColor"));
        Assert.Equal("\"#1f2937\"", after.ValuesByKey["brandColor"]);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var workspaceStore = new InMemoryWorkspaceManagementStore();
        workspaceStore.AddTenant("tenant-a");
        _ = await workspaceStore.UpsertAsync("tenant-a", "workspace-a", "Workspace A", "team", true);

        var entitlementStore = new TemplateTestPluginEntitlementStore();
        await entitlementStore.SetEntitledAsync("template-alpha", true, "workspace-a", "tenant-a");
        await entitlementStore.SetEntitledAsync("template-beta", true, "workspace-a", "tenant-a");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Header")
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Header", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton(new BackendHostOptions
        {
            DefaultTenantKey = "tenant-a"
        });

        builder.Services.AddSingleton<IWorkspaceTemplateRegistryStore, InMemoryWorkspaceTemplateRegistryStore>();
        builder.Services.AddSingleton<IWorkspaceThemeSettingsStore, InMemoryWorkspaceThemeSettingsStore>();
        builder.Services.AddSingleton<IWorkspaceManagementStore>(workspaceStore);
        builder.Services.AddSingleton<IPluginEntitlementStore>(entitlementStore);

        builder.Services.AddSingleton<CachedWorkspaceTemplateResolutionService>();
        builder.Services.AddSingleton<IWorkspaceTemplateResolutionService>(
            sp => sp.GetRequiredService<CachedWorkspaceTemplateResolutionService>());
        builder.Services.AddSingleton<IWorkspaceTemplateResolutionCache>(
            sp => sp.GetRequiredService<CachedWorkspaceTemplateResolutionService>());

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapThemeEndpoints();
        app.MapWorkspaceThemeEndpoints();
        await app.StartAsync();
        return app;
    }
}
