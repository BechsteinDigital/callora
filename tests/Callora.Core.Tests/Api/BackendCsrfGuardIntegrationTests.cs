using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using System.Net;
using Xunit;

namespace Callora.Core.Tests.Api;

public sealed class BackendCsrfGuardIntegrationTests
{
    private const string CookieName = "callora_admin_auth"; // BackendHostOptions default

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.UseBackendCsrfGuard(new BackendHostOptions());
        app.MapPost("/api/x", () => Results.Ok());
        app.MapGet("/api/x", () => Results.Ok());
        await app.StartAsync();
        return app;
    }

    private static HttpRequestMessage Build(HttpMethod method, string? origin, bool withCookie)
    {
        var request = new HttpRequestMessage(method, "/api/x");
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        if (withCookie)
        {
            request.Headers.Add("Cookie", $"{CookieName}=any-token");
        }

        return request;
    }

    [Fact]
    public async Task CrossOriginCookiePost_IsRejected()
    {
        await using var app = await StartAsync();
        var response = await app.GetTestClient()
            .SendAsync(Build(HttpMethod.Post, "https://evil.test", withCookie: true));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SameOriginCookiePost_PassesThrough()
    {
        await using var app = await StartAsync();
        var response = await app.GetTestClient()
            .SendAsync(Build(HttpMethod.Post, "http://localhost", withCookie: true));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CrossOriginPostWithoutCookie_PassesThrough()
    {
        // No auth cookie => header-authenticated API client => not a CSRF vector.
        await using var app = await StartAsync();
        var response = await app.GetTestClient()
            .SendAsync(Build(HttpMethod.Post, "https://evil.test", withCookie: false));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CrossOriginCookieGet_PassesThrough()
    {
        // Safe method: no state change to forge.
        await using var app = await StartAsync();
        var response = await app.GetTestClient()
            .SendAsync(Build(HttpMethod.Get, "https://evil.test", withCookie: true));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
