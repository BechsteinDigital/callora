using Callora.Administration.Api;
using Callora.Core.Api;
using Callora.Core.Application.Extensions;
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

        var response = await client.GetAsync("/api/ext/admin/plugins/voip/sip-accounts/sip-main?workspaceKey=ws-42");
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
    public async Task ProxyRoute_PlatformOperator_MustSelectAWorkspace()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        // No workspace key → platform operator; not bound to a single workspace.
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "sipaccount.read");

        var response = await client.GetAsync("/api/ext/admin/plugins/voip/whoami");

        // A workspace-scoped route without a resolvable workspace is a bad request,
        // not an ungated pass into the plugin (#109).
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProxyRoute_PlatformOperator_QuerySelectedWorkspace_ReachesTheHandler()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "sipaccount.read");

        var response = await client.GetAsync("/api/ext/admin/plugins/voip/whoami?workspaceKey=ws-9");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("ws-9", payload!["workspaceKey"]);
    }

    [Fact]
    public async Task ProxyRoute_WorkspaceBoundCaller_CannotOverrideItsWorkspaceByQuery()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "sipaccount.read");
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "ws-42");

        var response = await client.GetAsync("/api/ext/admin/plugins/voip/whoami?workspaceKey=ws-9");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("ws-42", payload!["workspaceKey"]);
    }

    [Fact]
    public async Task ProxyRoute_GlobalRoute_NeedsNoWorkspaceAndSkipsTheGate()
    {
        var evaluatorQueried = false;
        await using var app = await CreateAppAsync(
            availabilityEvaluator: new RecordingPluginAvailabilityEvaluator(() => evaluatorQueried = true));
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "sipaccount.read");

        var response = await client.GetAsync("/api/ext/admin/plugins/voip/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(evaluatorQueried); // an explicitly global route opts out of the workspace gate
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
    public async Task ProxyRoute_PlatformOperator_QuerySelectedWorkspace_IsAvailabilityGated()
    {
        var gatedWorkspaces = new List<string>();
        await using var app = await CreateAppAsync(
            availabilityEvaluator: new RecordingPluginAvailabilityEvaluator(gatedWorkspaces.Add));
        var client = app.GetTestClient();
        // A platform operator selects the target workspace; it must be gated exactly
        // like a token-bound one (#109).
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "sipaccount.read");

        var response = await client.GetAsync("/api/ext/admin/plugins/voip/sip-accounts/sip-main?workspaceKey=ws-9");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["ws-9"], gatedWorkspaces);
    }

    [Fact]
    public async Task ProxyRoute_PlatformOperator_UnentitledWorkspace_IsForbidden()
    {
        var handlerCalls = 0;
        await using var app = await CreateAppAsync(
            availabilityEvaluator: new StaticPluginAvailabilityEvaluator("voip"),
            onHandlerInvoked: () => Interlocked.Increment(ref handlerCalls));
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "sipaccount.read");

        var response = await client.GetAsync("/api/ext/admin/plugins/voip/sip-accounts/sip-main?workspaceKey=ws-9");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, handlerCalls);
    }

    [Fact]
    public async Task ProxyRoute_UnknownRoute_ReturnsNotFound()
    {
        await using var app = await CreateAppAsync(
            availabilityEvaluator: new StaticPluginAvailabilityEvaluator("voip"));
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "sipaccount.read");
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "ws-42");

        // No matching route → 404, unchanged and reached before any workspace gate.
        var response = await client.GetAsync("/api/ext/admin/plugins/voip/does-not-exist?workspaceKey=ws-42");

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

    // The admin UI chain (V1): which plugin bundles the admin shell may load for the
    // caller's effective workspace. Until this endpoint existed the shell loaded every
    // admin bundle in the manifest, so a plugin's UI appeared in workspaces it was never
    // assigned to.

    [Fact]
    public async Task UiChain_BoundCaller_ReturnsOnlyAvailablePluginsOfOwnWorkspace()
    {
        await using var app = await CreateAppAsync(
            availabilityEvaluator: new AllowListPluginAvailabilityEvaluator("voip"),
            activePluginIds: ["voip", "crm"]);
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "ws-42");

        var response = await client.GetAsync("/api/ext/admin/ui-chain");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<UiChainApiResponse>();
        Assert.NotNull(payload);
        Assert.Equal(["voip"], payload!.Chain);
    }

    [Fact]
    public async Task UiChain_BoundCaller_IgnoresForeignWorkspaceQuery()
    {
        // The same rule as the proxy route (#109): a bound session can never point the
        // chain at another workspace by naming it in the query.
        var queried = new List<string>();
        await using var app = await CreateAppAsync(
            availabilityEvaluator: new RecordingPluginAvailabilityEvaluator(queried.Add),
            activePluginIds: ["voip"]);
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "ws-42");

        var response = await client.GetAsync("/api/ext/admin/ui-chain?workspaceKey=ws-victim");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.All(queried, workspaceKey => Assert.Equal("ws-42", workspaceKey));
    }

    [Fact]
    public async Task UiChain_Operator_ResolvesTheSelectedWorkspace()
    {
        var queried = new List<string>();
        await using var app = await CreateAppAsync(
            availabilityEvaluator: new RecordingPluginAvailabilityEvaluator(queried.Add),
            activePluginIds: ["voip"]);
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "platform.operate");

        var response = await client.GetAsync("/api/ext/admin/ui-chain?workspaceKey=ws-7");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Contains("ws-7", queried);
    }

    [Fact]
    public async Task UiChain_WithoutResolvableWorkspace_ReturnsBadRequest()
    {
        await using var app = await CreateAppAsync(activePluginIds: ["voip"]);
        var client = app.GetTestClient();

        var response = await client.GetAsync("/api/ext/admin/ui-chain");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<WebApplication> CreateAppAsync(
        IPluginAvailabilityEvaluator? availabilityEvaluator = null,
        Action? onHandlerInvoked = null,
        IReadOnlyList<string>? activePluginIds = null)
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
                        }), request))),
                // Explicitly global: plugin-wide status that carries no workspace (#109).
                new HostAdminApiRouteRegistration(
                    "GET",
                    "status",
                    "sipaccount.read",
                    new StaticHostAdminApiRouteHandler(request => RunHandler(req =>
                        new HostAdminApiResponse(200, new Dictionary<string, string>
                        {
                            ["workspaceKey"] = req.WorkspaceKey ?? "(null)"
                        }), request)),
                    HostAdminApiRouteScope.Global)
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

        // The UI chain resolver the admin chain endpoint reads. Templates stay empty here —
        // the admin shell has a fixed structure and only the plugin part of the chain matters.
        builder.Services.AddScoped(sp => new WorkspaceUiChainResolver(
            new EmptyWorkspaceTemplateResolutionService(),
            new StaticWorkspacePluginActivationReader(activePluginIds ?? []),
            sp.GetService<IPluginAvailabilityEvaluator>() ?? new AllowListPluginAvailabilityEvaluator(
                (activePluginIds ?? []).ToArray())));

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
    private sealed class RecordingPluginAvailabilityEvaluator(Action<string> onEvaluated) : IPluginAvailabilityEvaluator
    {
        public RecordingPluginAvailabilityEvaluator(Action onEvaluated)
            : this(_ => onEvaluated())
        {
        }

        public Task<PluginAvailability> EvaluateAsync(
            string pluginId,
            string workspaceKey,
            CancellationToken cancellationToken = default)
        {
            onEvaluated(workspaceKey);
            return Task.FromResult(new PluginAvailability(true, Array.Empty<PluginAvailabilityFactor>()));
        }
    }

    /// <summary>Availability fake that answers "available" only for an allowlist.</summary>
    private sealed class AllowListPluginAvailabilityEvaluator(params string[] availablePluginIds)
        : IPluginAvailabilityEvaluator
    {
        public Task<PluginAvailability> EvaluateAsync(
            string pluginId,
            string workspaceKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PluginAvailability(
                availablePluginIds.Contains(pluginId, StringComparer.OrdinalIgnoreCase),
                Array.Empty<PluginAvailabilityFactor>()));
    }
}
