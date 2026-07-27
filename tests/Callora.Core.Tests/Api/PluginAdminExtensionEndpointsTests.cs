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
    public async Task ProxyRoute_WorkspaceScopedCaller_PluginUnavailable_ReturnsForbiddenWithoutInvokingHandler()
    {
        var handlerCalls = 0;
        await using var app = await CreateAppAsync(
            availabilityEvaluator: new StaticPluginAvailabilityEvaluator("voip"),
            onHandlerInvoked: () => Interlocked.Increment(ref handlerCalls));
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "sipaccount.read");
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "ws-42");

        var response = await client.GetAsync("/api/ext/admin/plugins/voip/sip-accounts/sip-main");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, handlerCalls); // the plugin route handler must not run when the plugin is dark
    }

    [Fact]
    public async Task ProxyRoute_WorkspaceScopedCaller_PluginAvailable_ExecutesHandler()
    {
        await using var app = await CreateAppAsync(
            availabilityEvaluator: new StaticPluginAvailabilityEvaluator(/* nothing unavailable */));
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "sipaccount.read");
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "ws-42");

        var response = await client.GetAsync("/api/ext/admin/plugins/voip/sip-accounts/sip-main");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("sip-main", payload!["sipAccountId"]);
    }

    [Fact]
    public async Task ProxyRoute_WithoutPermission_ReturnsForbiddenBeforeQueryingAvailability()
    {
        var evaluatorQueried = false;
        await using var app = await CreateAppAsync(
            availabilityEvaluator: new RecordingPluginAvailabilityEvaluator(() => evaluatorQueried = true));
        var client = app.GetTestClient();
        // No permission header → RBAC must reject before the availability gate.
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "ws-42");

        var response = await client.GetAsync("/api/ext/admin/plugins/voip/sip-accounts/sip-main");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(evaluatorQueried); // availability must not be evaluated for an unauthorized caller
    }

    [Fact]
    public async Task ProxyRoute_PlatformOperator_SkipsAvailabilityGate()
    {
        var evaluatorQueried = false;
        await using var app = await CreateAppAsync(
            availabilityEvaluator: new RecordingPluginAvailabilityEvaluator(() => evaluatorQueried = true));
        var client = app.GetTestClient();
        // No workspace key → platform operator (WorkspaceKey == null): no per-workspace gate.
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "sipaccount.read");

        var response = await client.GetAsync("/api/ext/admin/plugins/voip/sip-accounts/sip-main");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(evaluatorQueried); // no workspace scope → availability is skipped entirely
    }

    [Fact]
    public async Task ProxyRoute_UnknownRoute_ReturnsNotFound()
    {
        await using var app = await CreateAppAsync(
            availabilityEvaluator: new StaticPluginAvailabilityEvaluator("voip"));
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "sipaccount.read");
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "ws-42");

        // No matching route → 404, unchanged and reached before any availability gate.
        var response = await client.GetAsync("/api/ext/admin/plugins/voip/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    private static async Task<WebApplication> CreateAppAsync(
        IPluginAvailabilityEvaluator? availabilityEvaluator = null,
        Action? onHandlerInvoked = null)
    {
        HostAdminApiResponse RunHandler(Func<HostAdminApiRequest, HostAdminApiResponse> body, HostAdminApiRequest request)
        {
            onHandlerInvoked?.Invoke();
            return body(request);
        }

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
                    new StaticHostAdminApiRouteHandler(request => RunHandler(req =>
                    {
                        var sipAccountId = req.RouteValues.TryGetValue("sipAccountId", out var value)
                            ? value
                            : string.Empty;
                        return new HostAdminApiResponse(200, new Dictionary<string, string>
                        {
                            ["sipAccountId"] = sipAccountId
                        });
                    }, request))),
                // Echoes the workspace the dispatcher resolved for the caller, to assert scope flow.
                new HostAdminApiRouteRegistration(
                    "GET",
                    "whoami",
                    "sipaccount.read",
                    new StaticHostAdminApiRouteHandler(request => RunHandler(req =>
                        new HostAdminApiResponse(200, new Dictionary<string, string>
                        {
                            ["workspaceKey"] = req.WorkspaceKey ?? "(null)"
                        }), request)))
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
        if (availabilityEvaluator is not null)
        {
            builder.Services.AddSingleton(availabilityEvaluator);
        }

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapPluginAdminExtensionEndpoints();
        app.MapRbacEndpoints();
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// Records whether the availability gate queried it, so ordering assertions
    /// can prove the gate is (or is not) reached for a given caller.
    /// </summary>
    private sealed class RecordingPluginAvailabilityEvaluator(Action onEvaluated) : IPluginAvailabilityEvaluator
    {
        public Task<PluginAvailability> EvaluateAsync(
            string pluginId,
            string workspaceKey,
            CancellationToken cancellationToken = default)
        {
            onEvaluated();
            return Task.FromResult(new PluginAvailability(true, Array.Empty<PluginAvailabilityFactor>()));
        }
    }
}
