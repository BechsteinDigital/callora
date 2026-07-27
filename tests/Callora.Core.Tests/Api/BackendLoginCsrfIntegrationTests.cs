using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace Callora.Core.Tests.Api;

/// <summary>
/// Verifies <see cref="BackendLoginCsrfProtection.RequireSameOriginLogin"/> as an endpoint
/// filter: the cookie-issuing login POST is same-origin-guarded even though no auth cookie
/// exists yet, while non-browser clients keep working.
/// </summary>
public sealed class BackendLoginCsrfIntegrationTests
{
    private static async Task<WebApplication> StartAsync(params string[] allowedCsrfOrigins)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new BackendHostOptions { AllowedCsrfOrigins = allowedCsrfOrigins });
        var app = builder.Build();
        app.MapPost("/api/auth/login", () => Results.Ok()).RequireSameOriginLogin();
        await app.StartAsync();
        return app;
    }

    private static HttpRequestMessage Post(string? origin, string? referer = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login");
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        if (referer is not null)
        {
            request.Headers.Add("Referer", referer);
        }

        return request;
    }

    [Fact]
    public async Task CrossOriginBrowserLogin_IsRejected()
    {
        await using var app = await StartAsync();
        var response = await app.GetTestClient().SendAsync(Post("https://evil.test"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SameOriginLogin_PassesThrough()
    {
        await using var app = await StartAsync();
        var response = await app.GetTestClient().SendAsync(Post("http://localhost"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NonBrowserLoginWithoutOrigin_PassesThrough()
    {
        // No Origin/Referer => programmatic client (curl, mobile) => must keep working.
        await using var app = await StartAsync();
        var response = await app.GetTestClient().SendAsync(Post(origin: null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpaqueNullOriginLogin_IsRejected()
    {
        await using var app = await StartAsync();
        var response = await app.GetTestClient().SendAsync(Post("null"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CrossOriginRefererLogin_IsRejected_WhenOriginAbsent()
    {
        // The filter must fall back to the Referer's origin when no Origin header is sent.
        await using var app = await StartAsync();
        var response = await app.GetTestClient()
            .SendAsync(Post(origin: null, referer: "https://evil.test/login"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ExplicitlyAllowedOriginLogin_PassesThrough()
    {
        // Split-origin deployment: the admin shell is served from a different host than the API;
        // the filter must honour BackendHostOptions.AllowedCsrfOrigins resolved from DI.
        await using var app = await StartAsync("https://shell.example.com");
        var response = await app.GetTestClient().SendAsync(Post("https://shell.example.com"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
