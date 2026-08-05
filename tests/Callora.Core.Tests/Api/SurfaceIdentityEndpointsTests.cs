using System.Net;
using System.Net.Http.Json;
using Callora.Administration.Api;
using Callora.Core.Application.Persistence;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Plugins;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Core.Tests.Api;

/// <summary>The operator HTTP surface for a surface's identity provider (ADR-017 §5).</summary>
public sealed class SurfaceIdentityEndpointsTests
{
    private const string Workspace = "workspace-a";
    private const string Surface = "portal";
    private const string BasePath =
        $"/api/surfaces/workspaces/{Workspace}/surfaces/{Surface}/identity";

    [Fact]
    public async Task Candidates_OnlyListPluginsThatCanProvideIdentity()
    {
        await using var app = await CreateAppAsync();

        var candidates = await Client(app)
            .GetFromJsonAsync<SurfaceIdentityProviderCandidateApiResponse[]>($"{BasePath}/candidates");

        var candidate = Assert.Single(candidates!);
        Assert.Equal("crm", candidate.PluginId);
    }

    [Fact]
    public async Task Assign_StoresTheProviderAndReportsItsAvailability()
    {
        await using var app = await CreateAppAsync();

        var response = await Client(app)
            .PutAsJsonAsync(BasePath, new SurfaceIdentityAssignmentUpsertApiRequest("crm"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var assignment = await response.Content.ReadFromJsonAsync<SurfaceIdentityAssignmentApiResponse>();
        Assert.Equal("crm", assignment!.IdentityPluginId);
        Assert.Equal("header-user", assignment.AssignedBy);
        Assert.True(assignment.IsAvailable);
    }

    [Fact]
    public async Task Assign_RefusesAPluginWithoutTheCapability()
    {
        await using var app = await CreateAppAsync();

        var response = await Client(app)
            .PutAsJsonAsync(BasePath, new SurfaceIdentityAssignmentUpsertApiRequest("communication"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Clear_RemovesTheProvider()
    {
        await using var app = await CreateAppAsync();
        var client = Client(app);
        _ = await client.PutAsJsonAsync(BasePath, new SurfaceIdentityAssignmentUpsertApiRequest("crm"));

        var response = await client.DeleteAsync(BasePath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var assignment = await response.Content.ReadFromJsonAsync<SurfaceIdentityAssignmentApiResponse>();
        Assert.Null(assignment!.IdentityPluginId);
    }

    [Fact]
    public async Task AWorkspaceOutsideTheConfiguredTenant_IsNotFound()
    {
        await using var app = await CreateAppAsync();

        var response = await Client(app)
            .GetAsync("/api/surfaces/workspaces/foreign/surfaces/portal/identity");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WithoutTheExtensionPermission_TheAssignmentIsForbidden()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "extension.read");

        var response = await client
            .PutAsJsonAsync(BasePath, new SurfaceIdentityAssignmentUpsertApiRequest("crm"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpClient Client(WebApplication app)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "extension.read,extension.update");
        return client;
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var workspaces = new InMemoryWorkspaceManagementStore();
        workspaces.AddTenant("tenant-a");
        _ = await workspaces.UpsertAsync("tenant-a", Workspace, "Workspace A", "spa", true);

        var surfaces = new InMemoryWorkspaceSurfaceStore();
        _ = await surfaces.UpsertAsync(Workspace, new WorkspaceSurfaceInput(
            Surface, "Portal", "spa", null, null, "/", SurfaceAccessMode.Mixed,
            "de", null, null, null, null, true));

        var installations = new InMemoryPluginInstallationRepository();
        await AddPluginAsync(installations, "crm", SurfaceIdentityCapability.Key);
        await AddPluginAsync(installations, "communication", "communication.foundation");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Header")
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Header", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(new BackendHostOptions { DefaultTenantKey = "tenant-a" });
        builder.Services.AddSingleton<IWorkspaceManagementStore>(workspaces);
        builder.Services.AddSingleton<IWorkspaceSurfaceStore>(surfaces);
        builder.Services.AddSingleton<IPluginInstallationRepository>(installations);
        builder.Services.AddSingleton<IPluginAvailabilityEvaluator>(new StaticPluginAvailabilityEvaluator());
        builder.Services.AddSingleton<ISurfaceSessionStore, InMemorySurfaceSessionStore>();
        builder.Services.AddSingleton<SurfaceIdentityAssignmentService>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapSurfaceIdentityEndpoints();
        await app.StartAsync();
        return app;
    }

    private static Task AddPluginAsync(
        InMemoryPluginInstallationRepository installations,
        string pluginId,
        string capability)
    {
        var installation = PluginInstallation.CreateInstalled(
            pluginId, pluginId, $"/tmp/{pluginId}.dll", null, DateTimeOffset.UtcNow);
        installation.SetCapabilities([capability], null, null, DateTimeOffset.UtcNow);
        return installations.AddAsync(installation);
    }
}
