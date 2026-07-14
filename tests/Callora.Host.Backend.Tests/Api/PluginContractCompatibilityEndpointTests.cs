using Callora.Host.Backend.Application.Audit;
using Callora.Host.Backend.Application.Entitlements;
using System.Net;
using System.Net.Http.Json;
using Callora.Host.Backend.Api;
using Callora.Host.Workspace.Api;
using Callora.Host.Backend.Application.Plugins;
using Callora.Host.Backend.Application.Workspaces;
using Callora.Host.Backend.Application.Lifecycle;
using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Host.Backend.Tests.Api;

public sealed class PluginContractCompatibilityEndpointTests
{
    [Fact]
    public async Task GetCompatibility_ReturnsCompatibleVersion()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/api/plugins/contracts/compatibility");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PluginContractCompatibilityApiResponse[]>();
        Assert.NotNull(payload);
        Assert.Contains(payload, row => row.ContractVersion == "v2" && row.IsCompatible && row.Result == "compatible");
    }

    [Fact]
    public async Task GetCompatibility_ReturnsIncompatibleVersion()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/api/plugins/contracts/compatibility");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PluginContractCompatibilityApiResponse[]>();
        Assert.NotNull(payload);
        Assert.Contains(payload, row => row.ContractVersion == "v0" && !row.IsCompatible && row.Result == "incompatible");
    }

    [Fact]
    public async Task GetSupport_ReturnsDeprecatedAndRemovedSupportStates()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/api/plugins/contracts/support");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PluginContractSupportApiResponse[]>();
        Assert.NotNull(payload);
        Assert.Contains(payload, row => row.ContractVersion == "v1" && row.EmitsWarning);
        Assert.Contains(payload, row => row.ContractVersion == "v0" && !row.IsInstallable);
    }

    [Fact]
    public async Task GetTrustedSigners_ReturnsConfiguredPublisherMetadata()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/api/plugins/security/trusted-signers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TrustedPluginSignerApiResponse[]>();
        Assert.NotNull(payload);
        Assert.Contains(payload, row =>
            row.PublisherId == "acme-telephony" &&
            row.DisplayName == "Acme Telephony GmbH" &&
            row.Thumbprint == "AABBCCDDEEFF00112233445566778899AABBCCDD");
    }

    [Fact]
    public async Task GetWorkspaceEntitlementStatus_ReturnsDifferentStatusPerWorkspace()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var workspaceA = await client.GetFromJsonAsync<PluginWorkspaceEntitlementApiResponse>(
            "/api/plugins/workspaces/workspace-a/entitlements/voip");
        var workspaceB = await client.GetFromJsonAsync<PluginWorkspaceEntitlementApiResponse>(
            "/api/plugins/workspaces/workspace-b/entitlements/voip");

        Assert.NotNull(workspaceA);
        Assert.NotNull(workspaceB);
        Assert.True(workspaceA!.IsEntitled);
        Assert.False(workspaceB!.IsEntitled);
    }

    [Fact]
    public async Task GetTenantEntitlementStatus_LegacyRouteAlias_RemainsSupported()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetFromJsonAsync<PluginWorkspaceEntitlementApiResponse>(
            "/api/plugins/tenants/tenant-a/entitlements/voip");

        Assert.NotNull(response);
        Assert.True(response!.IsEntitled);
        Assert.Equal("tenant-a", response.WorkspaceKey);
        Assert.Equal("tenant-a", response.TenantKey);
    }

    [Fact]
    public async Task GetTenantEntitlementStatus_LegacyRouteAlias_OtherTenant_ReturnsNotFound()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/api/plugins/tenants/tenant-b/entitlements/voip");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCompatibility_WithoutPluginReadPermission_ReturnsForbidden()
    {
        await using var app = await CreateAppAsync(useHeaderAuth: true);
        var client = app.GetTestClient();

        var response = await client.GetAsync("/api/plugins/contracts/compatibility");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCompatibility_WithPluginReadPermission_ReturnsOk()
    {
        await using var app = await CreateAppAsync(useHeaderAuth: true);
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "plugin.read");

        var response = await client.GetAsync("/api/plugins/contracts/compatibility");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Install_WithOnlyPluginReadPermission_ReturnsForbidden()
    {
        await using var app = await CreateAppAsync(useHeaderAuth: true);
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "plugin.read");

        var response = await client.PostAsJsonAsync(
            "/api/plugins/install",
            new InstallPluginRequest("/tmp/plugin.dll", null, "tester"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Install_WithPluginCreatePermission_IsAuthorized()
    {
        await using var app = await CreateAppAsync(useHeaderAuth: true);
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "plugin.create");

        var response = await client.PostAsJsonAsync(
            "/api/plugins/install",
            new InstallPluginRequest("/tmp/plugin.dll", null, "tester"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<WebApplication> CreateAppAsync(bool useHeaderAuth = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        if (useHeaderAuth)
        {
            builder.Services
                .AddAuthentication("Header")
                .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Header", _ => { });
        }
        else
        {
            builder.Services
                .AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        }
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IPluginLifecycleService, StaticPluginLifecycleService>();
        builder.Services.AddSingleton<IHostAuditStore, InMemoryHostAuditStore>();
        var options = new BackendHostOptions
        {
            DefaultTenantKey = "tenant-a"
        };
        var entitlements = new InMemoryPluginEntitlementStore(options);
        await entitlements.SetEntitledAsync("voip", true, "workspace-a", "tenant-a");
        var workspaceStore = new InMemoryWorkspaceManagementStore();
        workspaceStore.AddTenant("tenant-a");
        _ = await workspaceStore.UpsertAsync("tenant-a", "workspace-a", "Workspace A", "team", true);
        _ = await workspaceStore.UpsertAsync("tenant-a", "workspace-b", "Workspace B", "team", true);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IPluginEntitlementStore>(entitlements);
        builder.Services.AddSingleton<IWorkspaceManagementStore>(workspaceStore);
        builder.Services.AddSingleton<IPluginSignatureTrustStore>(new StaticPluginSignatureTrustStore
        {
            Signers =
            [
                new TrustedPluginSigner(
                    PublisherId: "acme-telephony",
                    DisplayName: "Acme Telephony GmbH",
                    Thumbprint: "AABBCCDDEEFF00112233445566778899AABBCCDD",
                    Source: "marketplace-sync")
            ]
        });

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapPluginEndpoints();

        await app.StartAsync();
        return app;
    }
}
