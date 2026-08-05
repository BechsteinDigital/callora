using Callora.Administration.Api;
using Callora.Core.Api;
using Callora.Core.Application.Audit;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Callora.Core.Tests.Api;

/// <summary>
/// Session revocation over the real authentication pipeline (#105): sign in, take
/// the token, apply a revocation event, and prove the same token no longer works.
/// </summary>
public sealed class SessionRevocationEndpointTests
{
    private const string OperatorId = "root";
    private const string OperatorPassword = "operator-password-1";

    [Fact]
    public async Task IssuedToken_Works_UntilItIsRevoked()
    {
        await using var app = await CreateAppAsync();
        var token = await SignInAsync(app);

        Assert.Equal(HttpStatusCode.OK, (await GetMeAsync(app, token)).StatusCode);
    }

    [Fact]
    public async Task Logout_InvalidatesTheServerSideSession()
    {
        await using var app = await CreateAppAsync();
        var token = await SignInAsync(app);

        var logout = app.GetTestClient();
        logout.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.NoContent, (await logout.PostAsync("/api/auth/logout", null)).StatusCode);

        // Clearing the browser cookie is not enough — a copied bearer token must die too.
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync(app, token)).StatusCode);
    }

    [Fact]
    public async Task Logout_LeavesTheAccountsOtherSessionsAlive()
    {
        await using var app = await CreateAppAsync();
        var first = await SignInAsync(app);
        var second = await SignInAsync(app);

        var logout = app.GetTestClient();
        logout.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first);
        await logout.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync(app, first)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetMeAsync(app, second)).StatusCode);
    }

    [Fact]
    public async Task PasswordChange_InvalidatesEveryOutstandingSession()
    {
        await using var app = await CreateAppAsync();
        var token = await SignInAsync(app);

        var store = app.Services.GetRequiredService<IBackendUserStore>();
        await store.UpsertCredentialsAsync(OperatorId, null, null, "replacement-password-1");

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync(app, token)).StatusCode);
    }

    [Fact]
    public async Task Deactivation_InvalidatesEveryOutstandingSession()
    {
        await using var app = await CreateAppAsync();
        var token = await SignInAsync(app);

        var store = app.Services.GetRequiredService<IBackendUserStore>();
        await store.SetEnabledAsync(OperatorId, enabled: false);

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync(app, token)).StatusCode);
    }

    [Fact]
    public async Task DisabledAccount_CannotSignInAgain()
    {
        await using var app = await CreateAppAsync();
        var store = app.Services.GetRequiredService<IBackendUserStore>();
        await store.SetEnabledAsync(OperatorId, enabled: false);

        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/auth/login",
            new LoginApiRequest(OperatorId, OperatorPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Deletion_InvalidatesEveryOutstandingSession()
    {
        await using var app = await CreateAppAsync();
        var token = await SignInAsync(app);

        var store = app.Services.GetRequiredService<IBackendUserStore>();
        await store.RemoveAsync(OperatorId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync(app, token)).StatusCode);
    }

    [Fact]
    public async Task OperatorLogin_IsRefused_WhenExternalIdentityIsRequired()
    {
        await using var app = await CreateAppAsync(requireExternalIdentityForOperators: true);

        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/auth/login",
            new LoginApiRequest(OperatorId, OperatorPassword));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static Task<HttpResponseMessage> GetMeAsync(WebApplication app, string token)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.GetAsync("/api/auth/me");
    }

    private static async Task<string> SignInAsync(WebApplication app)
    {
        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/auth/login",
            new LoginApiRequest(OperatorId, OperatorPassword));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginApiResponse>();
        Assert.NotNull(payload);
        return payload!.AccessToken;
    }

    private static async Task<WebApplication> CreateAppAsync(bool requireExternalIdentityForOperators = false)
    {
        var options = new BackendHostOptions
        {
            JwtIssuer = "callora-tests",
            JwtAudience = "callora-host-api",
            JwtSigningKey = "callora-tests-signing-key-callora-tests-signing-key",
            EnableBootstrapApiKeys = false,
            RequireExternalIdentityForOperators = requireExternalIdentityForOperators,
            OidcAuthority = requireExternalIdentityForOperators ? "https://login.example.test" : null,
            RbacUserAssignments =
            [
                new BackendRbacUserAssignmentOptions { UserId = OperatorId, Role = BackendRoles.SuperAdmin }
            ]
        };

        var userStore = new InMemoryBackendUserStore();
        await userStore.UpsertCredentialsAsync(OperatorId, "root@example.test", "Root", OperatorPassword);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<BackendSessionStateCache>();
        builder.Services.AddSingleton<IBackendUserStore>(provider => new SessionStateInvalidatingUserStore(
            userStore,
            provider.GetRequiredService<BackendSessionStateCache>()));
        builder.Services.AddSingleton<IBackendSessionRevocationStore, InMemorySessionRevocationStore>();
        builder.Services.AddScoped<IBackendSessionValidator, BackendSessionValidator>();
        builder.Services.AddSingleton<IUserDataSubjectService>(new InMemoryUserDataSubjectService(userStore));
        builder.Services.AddSingleton<IHostAuditStore, InMemoryHostAuditStore>();
        builder.Services.AddSingleton<IBusinessEventBus>(new RecordingBusinessEventBus());
        builder.Services.AddBackendApiSecurity(options);
        builder.Services.AddBackendRateLimiting(options);

        var app = builder.Build();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAuthEndpoints();
        app.MapUserEndpoints();
        await app.StartAsync();
        return app;
    }
}
