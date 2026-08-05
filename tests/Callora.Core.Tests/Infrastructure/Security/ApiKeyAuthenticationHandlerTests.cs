using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using System.Net;
using System.Security.Claims;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Security;

/// <summary>
/// Credential validity must depend on the presented key alone. No configuration
/// permutation may turn an unknown key into a valid one (#103).
/// </summary>
public sealed class ApiKeyAuthenticationHandlerTests
{
    private const string HeaderName = "X-Callora-Api-Key";
    private const string KnownKey = "callora-bootstrap-known-key";

    public static TheoryData<bool, bool> ConfigurationMatrix() => new()
    {
        { true, true },
        { true, false },
        { false, true },
        { false, false }
    };

    [Theory]
    [MemberData(nameof(ConfigurationMatrix))]
    public async Task UnknownKey_IsRejected_InEveryConfiguration(
        bool enableBootstrapApiKeys,
        bool requireApiKeyAuthentication)
    {
        var options = CreateOptions(enableBootstrapApiKeys, requireApiKeyAuthentication);
        await using var app = await CreateAppAsync(options);

        var response = await SendAsync(app, "definitely-not-a-configured-key");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ConfigurationMatrix))]
    public async Task EmptyKeyHeader_IsRejected_InEveryConfiguration(
        bool enableBootstrapApiKeys,
        bool requireApiKeyAuthentication)
    {
        var options = CreateOptions(enableBootstrapApiKeys, requireApiKeyAuthentication);
        await using var app = await CreateAppAsync(options);

        var response = await SendAsync(app, string.Empty);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task KnownKey_AuthenticatesRegardlessOfRequireFlag(bool requireApiKeyAuthentication)
    {
        var options = CreateOptions(enableBootstrapApiKeys: true, requireApiKeyAuthentication);
        await using var app = await CreateAppAsync(options);

        var response = await SendAsync(app, KnownKey);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("bootstrap-api-key", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task KnownKey_IsRejected_WhenBootstrapKeysAreDisabled(bool requireApiKeyAuthentication)
    {
        var options = CreateOptions(enableBootstrapApiKeys: false, requireApiKeyAuthentication);
        await using var app = await CreateAppAsync(options);

        var response = await SendAsync(app, KnownKey);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RetiringTheKeyList_RevokesTheBootstrapCredential()
    {
        var options = CreateOptions(enableBootstrapApiKeys: true, requireApiKeyAuthentication: false);
        options.ApiKeys = [];
        await using var app = await CreateAppAsync(options);

        var response = await SendAsync(app, KnownKey);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BootstrapKey_IsRejected_AfterTheConfiguredExpiry()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero));
        var options = CreateOptions(enableBootstrapApiKeys: true, requireApiKeyAuthentication: true);
        options.BootstrapApiKeysExpireAtUtc = clock.GetUtcNow().AddHours(1);
        await using var app = await CreateAppAsync(options, clock);

        var beforeExpiry = await SendAsync(app, KnownKey);
        clock.Advance(TimeSpan.FromHours(2));
        var afterExpiry = await SendAsync(app, KnownKey);

        Assert.Equal(HttpStatusCode.OK, beforeExpiry.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterExpiry.StatusCode);
    }

    [Fact]
    public async Task KeyWithMatchingPrefix_IsRejected()
    {
        var options = CreateOptions(enableBootstrapApiKeys: true, requireApiKeyAuthentication: false);
        await using var app = await CreateAppAsync(options);

        var response = await SendAsync(app, KnownKey[..^1]);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static BackendHostOptions CreateOptions(
        bool enableBootstrapApiKeys,
        bool requireApiKeyAuthentication) => new()
        {
            JwtIssuer = "callora-tests",
            JwtAudience = "callora-host-api",
            JwtSigningKey = "callora-tests-signing-key-callora-tests-signing-key",
            ApiKeyHeaderName = HeaderName,
            EnableBootstrapApiKeys = enableBootstrapApiKeys,
            RequireApiKeyAuthentication = requireApiKeyAuthentication,
            ApiKeys = [KnownKey]
        };

    private static async Task<WebApplication> CreateAppAsync(
        BackendHostOptions options,
        TimeProvider? timeProvider = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        if (timeProvider is not null)
        {
            builder.Services.AddSingleton(timeProvider);
        }

        builder.Services.AddSingleton(options);
        builder.Services.AddBackendApiSecurity(options);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/probe", (ClaimsPrincipal user) => user.Identity?.Name ?? "anonymous")
            .RequireAuthorization();
        await app.StartAsync();
        return app;
    }

    private static Task<HttpResponseMessage> SendAsync(WebApplication app, string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.TryAddWithoutValidation(HeaderName, apiKey);
        return app.GetTestClient().SendAsync(request);
    }
}
