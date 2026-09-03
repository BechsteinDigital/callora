using Callora.Administration.Api.Admin.WorkspacePlugins;
using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.WorkspaceAssignments;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace Callora.Core.Tests.Api;

public sealed class WorkspacePluginsControllerTests
{
    [Fact]
    public async Task List_WithoutAuthentication_ReturnsUnauthorized()
    {
        await using var app = await CreateAppAsync();

        var response = await app.GetTestClient().GetAsync("/api/workspaces/acme/plugins");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_AsSuperAdmin_ReturnsInstalledPlugins()
    {
        var options = CreateOptions();
        await using var app = await CreateAppAsync(options);
        var client = app.GetTestClient();
        Authenticate(client, CreateJwt(options));

        var response = await client.GetAsync("/api/workspaces/acme/plugins");
        var items = await response.Content.ReadFromJsonAsync<WorkspacePluginAssignmentApiResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.Single(items!);
        Assert.Equal("videoconference", item.PluginId);
        Assert.False(item.IsAssigned);
    }

    [Fact]
    public async Task Put_AssignsPluginAndReturnsEffectiveState()
    {
        var options = CreateOptions();
        await using var app = await CreateAppAsync(options);
        var client = app.GetTestClient();
        Authenticate(client, CreateJwt(options));

        var response = await client.PutAsJsonAsync(
            "/api/workspaces/acme/plugins/videoconference",
            new SetWorkspacePluginAssignmentApiRequest { IsAssigned = true });
        var item = await response.Content.ReadFromJsonAsync<WorkspacePluginAssignmentApiResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(item!.IsEntitled);
        Assert.True(item.IsActive);
        Assert.True(item.IsAssigned);
    }

    [Fact]
    public async Task List_UnknownWorkspace_ReturnsNotFound()
    {
        var options = CreateOptions();
        await using var app = await CreateAppAsync(options);
        var client = app.GetTestClient();
        Authenticate(client, CreateJwt(options));

        var response = await client.GetAsync("/api/workspaces/missing/plugins");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static BackendHostOptions CreateOptions() => new()
    {
        JwtIssuer = "callora-tests",
        JwtAudience = "callora-host-api",
        JwtSigningKey = "callora-tests-signing-key-callora-tests-signing-key",
        EnableBootstrapApiKeys = false,
        RequireApiKeyAuthentication = true,
        ApiKeys = ["unused"],
    };

    private static async Task<WebApplication> CreateAppAsync(BackendHostOptions? options = null)
    {
        options ??= CreateOptions();
        var workspaceStore = new InMemoryWorkspaceManagementStore();
        workspaceStore.AddTenant("tenant-a");
        _ = await workspaceStore.UpsertAsync(
            "tenant-a",
            "acme",
            "Acme",
            "standard",
            isActive: true);
        workspaceStore.AddTenant("tenant-b");
        _ = await workspaceStore.UpsertAsync(
            "tenant-b",
            "nachbar",
            "Nachbar",
            "standard",
            isActive: true);
        var lifecycle = new ConfigurablePluginLifecycleService();
        lifecycle.Installations.Add(new PluginInstallationSnapshot(
            "videoconference",
            "Video Conference",
            "/plugins/videoconference.dll",
            null,
            State: 1,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow));
        var activations = new InMemoryWorkspacePluginActivationStore();
        var entitlements = new InMemoryPluginEntitlementStore(options);
        var assignmentService = new WorkspacePluginAssignmentService(
            workspaceStore,
            lifecycle,
            activations,
            entitlements,
            NullLogger<WorkspacePluginAssignmentService>.Instance);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(options);
        builder.Services.AddBackendApiSecurity(options);
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(WorkspacePluginsController).Assembly);
        builder.Services.AddSingleton(assignmentService);
        builder.Services.AddSingleton(new WorkspaceReach(workspaceStore));

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private static void Authenticate(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    /// <summary>
    /// Ein Mandanten-Administrator verwaltet die Workspaces seines Mandanten — und nur die.
    /// </summary>
    /// <remarks>
    /// <b>Der Befund:</b> Der Controller nahm den Workspace-Schlüssel aus der URL und fragte nie, ob
    /// der Aufrufer ihn erreichen darf. Das blieb folgenlos, solange nur Operatoren
    /// <c>plugin.execute</c> hielten; mit <c>plugin.assign</c> im Mandantensatz wäre es der Weg
    /// gewesen, den Nachbarn zu verwalten, indem man seinen Schlüssel in die URL schreibt. Der
    /// Write-Backstop in der Persistenz hätte den Schreibzugriff zwar abgefangen — als 500, und die
    /// Lesesicht gar nicht.
    /// </remarks>
    [Fact]
    public async Task ATenantAdmin_ReachesItsOwnWorkspace_ButNotTheNeighbours()
    {
        var options = CreateOptions();
        await using var app = await CreateAppAsync(options);
        var client = app.GetTestClient();
        Authenticate(client, CreateTenantJwt(options, "tenant-a"));

        var own = await client.PutAsJsonAsync(
            "/api/workspaces/acme/plugins/videoconference",
            new SetWorkspacePluginAssignmentApiRequest { IsAssigned = true });
        var neighbour = await client.PutAsJsonAsync(
            "/api/workspaces/nachbar/plugins/videoconference",
            new SetWorkspacePluginAssignmentApiRequest { IsAssigned = true });

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, neighbour.StatusCode);
    }

    [Fact]
    public async Task ATenantAdmin_DoesNotListTheNeighboursPlugins()
    {
        var options = CreateOptions();
        await using var app = await CreateAppAsync(options);
        var client = app.GetTestClient();
        Authenticate(client, CreateTenantJwt(options, "tenant-a"));

        var response = await client.GetAsync("/api/workspaces/nachbar/plugins");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static string CreateTenantJwt(BackendHostOptions options, string tenantKey)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
                [
                    new Claim("sub", "tenant-admin"),
                    new Claim(BackendClaimTypes.CalloraScope, BackendAuthScopes.Tenant),
                    new Claim(BackendClaimTypes.TenantKey, tenantKey),
                    .. TenantRolePermissions
                        .ForRole(BackendRoles.Admin)
                        .Select(permission => new Claim(BackendClaimTypes.Permission, permission)),
                ]),
            Expires = DateTime.UtcNow.AddMinutes(30),
            Issuer = options.JwtIssuer,
            Audience = options.JwtAudience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(tokenHandler.CreateToken(descriptor));
    }

    private static string CreateJwt(BackendHostOptions options)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
                [
                    new Claim("sub", "operator"),
                    new Claim(ClaimTypes.Role, BackendRoles.SuperAdmin),
                ]),
            Expires = DateTime.UtcNow.AddMinutes(30),
            Issuer = options.JwtIssuer,
            Audience = options.JwtAudience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(tokenHandler.CreateToken(descriptor));
    }
}
