using Callora.Administration.Api;
using Callora.Core.Api;
using Callora.Core.Application.Audit;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Application.Security.Events;
using Callora.Core.Infrastructure.Security;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;

namespace Callora.Core.Tests.Api;

public sealed class AuthAndUserEndpointsTests
{
    [Fact]
    public async Task ApiLogin_WithOperatorRole_ReturnsPlatformScopedToken()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginApiRequest("root", "pass-root"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginApiResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.Equal("Bearer", payload.TokenType);
        Assert.Equal("root", payload.UserId);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(payload.AccessToken);
        Assert.Contains(token.Claims, x =>
            x.Type == BackendClaimTypes.CalloraScope && x.Value == BackendAuthScopes.Platform);
    }

    [Fact]
    public async Task ApiLogin_WithoutOperatorRole_ReturnsForbidden()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginApiRequest("alice", "pass-1"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WorkspaceLogin_WithoutMembership_ReturnsForbidden()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/workspace/auth/login",
            new WorkspaceLoginApiRequest("alice", "pass-1", "workspace-b"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WorkspaceLogin_WithMembership_ReturnsBearerTokenAndWorkspace()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/workspace/auth/login",
            new WorkspaceLoginApiRequest("alice", "pass-1", "workspace-a"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginApiResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.Equal("workspace-a", payload.WorkspaceKey);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(payload.AccessToken);
        Assert.Contains(token.Claims, x =>
            x.Type == BackendClaimTypes.CalloraScope && x.Value == BackendAuthScopes.Workspace);
    }

    [Fact]
    public async Task WorkspaceLogin_AsWorkspaceAdmin_IssuesScopedPermissions()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/workspace/auth/login",
            new WorkspaceLoginApiRequest("carol", "pass-carol", "workspace-a"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginApiResponse>();
        Assert.Equal(BackendRoles.Admin, payload!.Role);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(payload.AccessToken);
        var permissions = token.Claims
            .Where(x => x.Type == BackendClaimTypes.Permission)
            .Select(x => x.Value)
            .ToArray();

        Assert.Contains(BackendPermissionKeys.FlowManage, permissions);
        Assert.Contains(BackendPermissionKeys.UserRead, permissions);
        Assert.DoesNotContain("*", permissions);
        Assert.DoesNotContain(BackendPermissionKeys.TenantCreate, permissions);
    }

    [Fact]
    public async Task ApiLogin_AsWorkspaceMember_WithWorkspaceKey_ReturnsWorkspaceScopedToken()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginApiRequest("alice", "pass-1", "workspace-a"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginApiResponse>();
        Assert.NotNull(payload);
        Assert.Equal("workspace-a", payload!.WorkspaceKey);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(payload.AccessToken);
        Assert.Contains(token.Claims, x =>
            x.Type == BackendClaimTypes.CalloraScope && x.Value == BackendAuthScopes.Workspace);
    }

    [Fact]
    public async Task ApiLogin_AsWorkspaceAdmin_WithWorkspaceKey_IssuesScopedPermissions()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginApiRequest("carol", "pass-carol", "workspace-a"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginApiResponse>();
        Assert.Equal(BackendRoles.Admin, payload!.Role);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(payload.AccessToken);
        var permissions = token.Claims
            .Where(x => x.Type == BackendClaimTypes.Permission)
            .Select(x => x.Value)
            .ToArray();

        Assert.Contains(BackendPermissionKeys.FlowManage, permissions);
        Assert.DoesNotContain("*", permissions);
    }

    [Fact]
    public async Task ApiLogin_AsWorkspaceMember_ForeignWorkspace_ReturnsForbidden()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginApiRequest("alice", "pass-1", "workspace-b"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApiLogin_AsOperator_WithWorkspaceKey_StaysPlatformScoped()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginApiRequest("root", "pass-root", "workspace-a"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginApiResponse>();
        Assert.Null(payload!.WorkspaceKey);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(payload.AccessToken);
        Assert.Contains(token.Claims, x =>
            x.Type == BackendClaimTypes.CalloraScope && x.Value == BackendAuthScopes.Platform);
    }

    [Fact]
    public async Task Users_List_AsWorkspaceUser_OnlyShowsOwnWorkspace()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "user.read");
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "workspace-a");

        var users = await client.GetFromJsonAsync<BackendUserApiResponse[]>("/api/users");

        var ids = users!.Select(x => x.ExternalId).ToArray();
        Assert.Contains("alice", ids);
        Assert.Contains("carol", ids);
        Assert.DoesNotContain("dave", ids);
        Assert.DoesNotContain("root", ids);
    }

    [Fact]
    public async Task Users_Get_ForeignWorkspaceUser_ReturnsNotFound()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "user.read");
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "workspace-a");

        var response = await client.GetAsync("/api/users/dave");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Users_DataExport_AsWorkspaceUser_IsForbidden()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "user.read");
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "workspace-a");

        // The export is a global identity operation (#102): it discloses every
        // membership of the subject, so it stays operator-only — even for a
        // member of the caller's own workspace.
        var foreign = await client.GetAsync("/api/users/dave/data-export");
        var own = await client.GetAsync("/api/users/alice/data-export");

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
    }

    [Fact]
    public async Task Users_List_AsOperator_ShowsAllUsers()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "user.read");
        client.DefaultRequestHeaders.Add("X-Test-Callora-Scope", "platform");

        var users = await client.GetFromJsonAsync<BackendUserApiResponse[]>("/api/users");

        var ids = users!.Select(x => x.ExternalId).ToArray();
        Assert.Contains("dave", ids);
        Assert.Contains("root", ids);
    }

    [Fact]
    public async Task Users_Create_AsWorkspaceUser_IsForbidden()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "user.create");
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "workspace-a");

        var response = await client.PostAsJsonAsync(
            "/api/users",
            new CreateBackendUserApiRequest("mallory", "mallory@example.test", "Mallory", "pass-m"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserCrud_WithPermissions_WorksEndToEnd()
    {
        await using var app = await CreateAppAsync();
        var bus = (RecordingBusinessEventBus)app.Services.GetRequiredService<IBusinessEventBus>();

        var createClient = app.GetTestClient();
        createClient.DefaultRequestHeaders.Add("X-Test-Permissions", "user.create");
        var createResponse = await createClient.PostAsJsonAsync(
            "/api/users",
            new CreateBackendUserApiRequest("bob", "bob@example.test", "Bob", "pass-2"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var listClient = app.GetTestClient();
        listClient.DefaultRequestHeaders.Add("X-Test-Permissions", "user.read");
        var listResponse = await listClient.GetFromJsonAsync<BackendUserApiResponse[]>("/api/users");
        Assert.NotNull(listResponse);
        Assert.Contains(listResponse!, x => x.ExternalId == "bob" && x.HasPassword);

        var updateClient = app.GetTestClient();
        updateClient.DefaultRequestHeaders.Add("X-Test-Permissions", "user.update");
        var updateResponse = await updateClient.PutAsJsonAsync(
            "/api/users/bob",
            new UpdateBackendUserApiRequest("bob-updated@example.test", "Bob Updated", null));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var deleteClient = app.GetTestClient();
        deleteClient.DefaultRequestHeaders.Add("X-Test-Permissions", "user.delete");
        var deleteResponse = await deleteClient.DeleteAsync("/api/users/bob");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Each lifecycle step publishes its business event.
        var names = bus.Published.Select(static x => x.EventName).ToArray();
        Assert.Contains(UserEventTypes.Created, names);
        Assert.Contains(UserEventTypes.Updated, names);
        Assert.Contains(UserEventTypes.Deleted, names);
    }

    /// <summary>
    /// Der Plugin-Schlüssel muss den Weg bis ins Token finden — nicht nur bis in den Dienst.
    /// </summary>
    /// <remarks>
    /// <b>Der Befund:</b> <c>WorkspaceSessionPermissions</c> stand als optionaler Parameter am
    /// privaten Helfer, aber keine Route band ihn. Der Helfer bekam also immer seinen Default
    /// <c>null</c>, und <c>AdminLoginResolver</c> fiel auf den festen Kern-Satz der Mitgliedsrolle
    /// zurück. Die Zusammensetzung war getestet, der Weg dorthin nicht — und im Betrieb zählt der
    /// Weg: Ein Workspace-Admin bekam die Schlüssel der Plugins seines Workspace nie zu sehen.
    /// <para>
    /// Deshalb prüft dieser Test über HTTP und liest das ausgestellte Token, statt den Resolver
    /// direkt zu fragen. Ein Test, der den Dienst selbst aufruft, wäre auch vorher grün gewesen.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task WorkspaceLogin_CarriesThePluginKeysOfTheWorkspace_NotJustTheCoreFloor()
    {
        await using var app = await CreateAppAsync(withPluginPermissions: true);
        var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/workspace/auth/login",
            new WorkspaceLoginApiRequest("carol", "pass-carol", "workspace-a"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginApiResponse>();

        var permissions = new JwtSecurityTokenHandler()
            .ReadJwtToken(payload!.AccessToken)
            .Claims
            .Where(x => x.Type == BackendClaimTypes.Permission)
            .Select(x => x.Value)
            .ToArray();

        Assert.Contains("pbx.person.read", permissions);
        Assert.Contains(BackendPermissionKeys.FlowManage, permissions);
    }

    [Fact]
    public async Task ApiLogin_AsTenantMember_ReturnsTenantScopedToken()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginApiRequest("carol", "pass-carol", WorkspaceKey: null, TenantKey: "tenant-a"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginApiResponse>();
        Assert.Equal("tenant-a", payload!.TenantKey);
        Assert.Null(payload.WorkspaceKey);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(payload.AccessToken);
        Assert.Contains(token.Claims, x =>
            x.Type == BackendClaimTypes.CalloraScope && x.Value == BackendAuthScopes.Tenant);
        Assert.Contains(token.Claims, x =>
            x.Type == BackendClaimTypes.TenantKey && x.Value == "tenant-a");

        var permissions = token.Claims
            .Where(x => x.Type == BackendClaimTypes.Permission)
            .Select(x => x.Value)
            .ToArray();

        Assert.Contains(BackendPermissionKeys.WorkspaceRead, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.PluginCreate, permissions);
    }

    private sealed class StubMembershipRoles : IWorkspaceMembershipRoleStore
    {
        public Task<IReadOnlyList<string>> ListRolesAsync(
            string workspaceKey, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>?> ReplaceRolesAsync(
            string workspaceKey,
            string userId,
            IReadOnlyCollection<string> roles,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Dieser Test weist nichts zu.");

        public Task<IReadOnlyList<string>> ListUsersWithRoleAsync(
            string role, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Dieser Test widerruft nichts.");
    }

    private sealed class StubActivations(IReadOnlyList<string> active)
        : Callora.Core.Application.Plugins.IWorkspacePluginActivationReader
    {
        public Task<IReadOnlyList<string>> ListActivePluginIdsAsync(
            string workspaceKey, CancellationToken cancellationToken = default)
            => Task.FromResult(active);
    }

    private sealed class StubPluginMap(Dictionary<string, IReadOnlyList<string>> byPlugin)
        : IPluginPermissionMap
    {
        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ByPluginAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(byPlugin);
    }

    private static async Task<WebApplication> CreateAppAsync(bool withPluginPermissions = false)
    {
        var options = new BackendHostOptions
        {
            JwtIssuer = "callora-tests",
            JwtAudience = "callora-host-api",
            JwtSigningKey = "callora-tests-signing-key-callora-tests-signing-key",
            RbacUserAssignments =
            [
                new BackendRbacUserAssignmentOptions { UserId = "root", Role = BackendRoles.SuperAdmin }
            ]
        };

        var userStore = new InMemoryBackendUserStore();
        await userStore.UpsertCredentialsAsync("alice", "alice@example.test", "Alice", "pass-1");
        await userStore.UpsertCredentialsAsync("carol", "carol@example.test", "Carol", "pass-carol");
        await userStore.UpsertCredentialsAsync("dave", "dave@example.test", "Dave", "pass-dave");
        await userStore.UpsertCredentialsAsync("root", "root@example.test", "Root", "pass-root");
        userStore.AddWorkspaceMember("workspace-a", "alice");
        userStore.AddWorkspaceMember("workspace-a", "carol", BackendRoles.Admin);
        userStore.AddWorkspaceMember("workspace-b", "dave");
        userStore.AddTenantMember("tenant-a", "carol", BackendRoles.Admin);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Header")
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Header", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IBackendUserStore>(userStore);
        builder.Services.AddSingleton<IUserDataSubjectService>(new InMemoryUserDataSubjectService(userStore));
        builder.Services.AddSingleton<IBackendRbacStore>(new InMemoryBackendRbacStore(options));
        builder.Services.AddSingleton<IBusinessEventBus>(new RecordingBusinessEventBus());
        builder.Services.AddSingleton<IHostAuditStore, InMemoryHostAuditStore>();

        if (withPluginPermissions)
        {
            builder.Services.AddSingleton(new WorkspaceSessionPermissions(
                new StubMembershipRoles(),
                new InMemoryBackendRbacStore(options),
                new WorkspacePluginPermissions(
                    new StubActivations(["pbx"]),
                    new StubPluginMap(new Dictionary<string, IReadOnlyList<string>>
                    {
                        ["pbx"] = ["pbx.person.read"]
                    }))));
        }

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAuthEndpoints();
        app.MapUserEndpoints();
        await app.StartAsync();
        return app;
    }
}
