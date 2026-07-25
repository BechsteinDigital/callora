using Callora.Administration.Api;
using Callora.Core.Api;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Callora.Core.Tests.Api;

public sealed class PluginAdminExtensionEndpointsTests
{
    [Fact]
    public async Task Navigation_WithPermission_ReturnsPluginEntries()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "sipaccount.read");

        var response = await client.GetAsync("/api/ext/admin/navigation");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PluginAdminNavigationApiResponse[]>();
        Assert.NotNull(payload);
        Assert.Contains(payload!, entry =>
            entry.PluginId == "voip" &&
            entry.Id == "voip-sip-accounts" &&
            entry.To == "/sip-accounts");
    }

    [Fact]
    public async Task Navigation_WithoutPermission_HidesProtectedEntries()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/api/ext/admin/navigation");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PluginAdminNavigationApiResponse[]>();
        Assert.NotNull(payload);
        Assert.DoesNotContain(payload!, entry => entry.Id == "voip-sip-accounts");
    }

    [Fact]
    public async Task ProxyRoute_WithPermission_ExecutesPluginHandler()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "sipaccount.read");

        var response = await client.GetAsync("/api/ext/admin/plugins/voip/sip-accounts/sip-main");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(payload);
        Assert.Equal("sip-main", payload!["sipAccountId"]);
    }

    [Fact]
    public async Task ProxyRoute_WithoutPermission_ReturnsForbidden()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/api/ext/admin/plugins/voip/sip-accounts/sip-main");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProxyRoute_WorkspaceScopedCaller_PassesBoundWorkspaceToHandler()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "sipaccount.read");
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "ws-42");

        var response = await client.GetAsync("/api/ext/admin/plugins/voip/whoami");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("ws-42", payload!["workspaceKey"]); // the caller's bound workspace, from the token
    }

    [Fact]
    public async Task ProxyRoute_PlatformOperator_HasNoBoundWorkspace()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        // No workspace key → platform operator; not bound to a single workspace.
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "sipaccount.read");

        var response = await client.GetAsync("/api/ext/admin/plugins/voip/whoami");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("(null)", payload!["workspaceKey"]);
    }

    [Fact]
    public async Task RbacPermissions_IncludePluginPermissions()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "role.read");

        var response = await client.GetAsync("/api/security/rbac/permissions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RbacPermissionApiResponse[]>();
        Assert.NotNull(payload);
        Assert.Contains(payload!, permission => permission.PermissionKey == "sipaccount.read");
        Assert.Contains(payload!, permission => permission.PermissionKey == "sipaccount.create");
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var contributor = new StaticHostAdminApiExtensionContributor
        {
            PluginId = "voip",
            PermissionKeys = ["sipaccount.create", "sipaccount.read", "sipaccount.update", "sipaccount.delete"],
            NavigationItems =
            [
                new HostAdminNavigationItem(
                    Id: "voip-sip-accounts",
                    Label: "SIP Accounts",
                    To: "/sip-accounts",
                    Icon: "i-lucide-phone-call",
                    Order: 10,
                    RequiredPermission: "sipaccount.read")
            ],
            Routes =
            [
                new HostAdminApiRouteRegistration(
                    "GET",
                    "sip-accounts/{sipAccountId}",
                    "sipaccount.read",
                    new StaticHostAdminApiRouteHandler(request =>
                    {
                        var sipAccountId = request.RouteValues.TryGetValue("sipAccountId", out var value)
                            ? value
                            : string.Empty;
                        return new HostAdminApiResponse(200, new Dictionary<string, string>
                        {
                            ["sipAccountId"] = sipAccountId
                        });
                    })),
                // Echoes the workspace the dispatcher resolved for the caller, to assert scope flow.
                new HostAdminApiRouteRegistration(
                    "GET",
                    "whoami",
                    "sipaccount.read",
                    new StaticHostAdminApiRouteHandler(request =>
                        new HostAdminApiResponse(200, new Dictionary<string, string>
                        {
                            ["workspaceKey"] = request.WorkspaceKey ?? "(null)"
                        })))
            ]
        };

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Header")
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Header", _ => { });
        builder.Services.AddAuthorization();
        // The dispatcher resolves the caller's workspace scope from the principal.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IWorkspaceScopeContext, HttpWorkspaceScopeContext>();
        builder.Services.AddSingleton<ICalloraPluginCatalog>(new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IHostAdminApiExtensionContributor)] = [contributor]
        }));

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapPluginAdminExtensionEndpoints();
        app.MapRbacEndpoints();
        await app.StartAsync();
        return app;
    }
}
