using System.Net;
using System.Net.Http.Json;
using Callora.Host.Backend.Api;
using Callora.Host.Workspace.Api;
using Callora.Host.Backend.Application.Abstractions.Security;
using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Infrastructure.Security;
using Callora.Host.Backend.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Host.Backend.Tests.Api;

public sealed class AuthAndUserEndpointsTests
{
    [Fact]
    public async Task ApiLogin_WithValidCredentials_ReturnsBearerToken()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginApiRequest("alice", "pass-1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginApiResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.Equal("Bearer", payload.TokenType);
        Assert.Equal("alice", payload.UserId);
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
    }

    [Fact]
    public async Task UserCrud_WithPermissions_WorksEndToEnd()
    {
        await using var app = await CreateAppAsync();

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
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var options = new BackendHostOptions
        {
            JwtIssuer = "callora-tests",
            JwtAudience = "callora-host-api",
            JwtSigningKey = "callora-tests-signing-key-callora-tests-signing-key"
        };

        var userStore = new InMemoryBackendUserStore();
        await userStore.UpsertCredentialsAsync("alice", "alice@example.test", "Alice", "pass-1");
        userStore.AddWorkspaceMember("workspace-a", "alice");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Header")
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Header", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IBackendUserStore>(userStore);
        builder.Services.AddSingleton<IBackendRbacStore>(new InMemoryBackendRbacStore(options));

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAuthEndpoints();
        app.MapUserEndpoints();
        await app.StartAsync();
        return app;
    }
}
