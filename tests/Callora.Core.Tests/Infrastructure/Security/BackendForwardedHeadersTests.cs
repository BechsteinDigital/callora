using System.Net;
using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Security;

/// <summary>
/// Behind a TLS-terminating proxy the app must honour X-Forwarded-Proto so the
/// same-origin CSRF check compares the browser's <c>https://</c> origin against an
/// <c>https://</c> request origin. Without it, the proxied <c>http</c> scheme makes
/// every cookie-authenticated mutation look cross-origin and get a 403 — the
/// production symptom (PUT /api/workspaces returned 403 behind Caddy).
/// </summary>
public sealed class BackendForwardedHeadersTests
{
    [Fact]
    public void Build_WithoutExplicitTrust_ClearsLoopbackDefaults()
    {
        var built = BackendForwardedHeaders.Build(new BackendForwardedHeadersOptions());

        // Cleared so a dynamic-address upstream (compose-internal proxy) is honoured.
        Assert.Empty(built.KnownProxies);
        Assert.Empty(built.KnownIPNetworks);
    }

    [Fact]
    public void Build_WithExplicitProxyAndNetwork_KeepsThem()
    {
        var built = BackendForwardedHeaders.Build(new BackendForwardedHeadersOptions
        {
            KnownProxies = ["10.0.0.5", "not-an-ip"],
            KnownNetworks = ["172.16.0.0/12", "garbage"],
        });

        Assert.Contains(built.KnownProxies, ip => ip.Equals(IPAddress.Parse("10.0.0.5")));
        Assert.Single(built.KnownProxies); // the invalid entry is dropped
        Assert.Single(built.KnownIPNetworks); // "garbage" dropped, CIDR kept
    }

    [Fact]
    public async Task CsrfGuard_WithForwardedProtoHttps_TreatsHttpsOriginAsSameOrigin()
    {
        await using var app = await CreateAppAsync(forwardedHeadersEnabled: true);
        var client = app.GetTestClient();

        var response = await client.SendAsync(BuildMutation());

        // Forwarded proto makes the request origin https://localhost == the browser Origin.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CsrfGuard_WithoutForwardedHeaders_RejectsAsCrossOrigin()
    {
        await using var app = await CreateAppAsync(forwardedHeadersEnabled: false);
        var client = app.GetTestClient();

        var response = await client.SendAsync(BuildMutation());

        // The proxied scheme stays http, so http://localhost != https://localhost → 403.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // A cookie-authenticated PUT from the browser: Origin is the public https origin,
    // the proxy forwards proto/host, and the admin auth cookie is attached.
    private static HttpRequestMessage BuildMutation()
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/guarded");
        request.Headers.Host = "localhost";
        request.Headers.Add("Origin", "https://localhost");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "localhost");
        request.Headers.Add("Cookie", "callora_admin_auth=session");
        return request;
    }

    private static async Task<WebApplication> CreateAppAsync(bool forwardedHeadersEnabled)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var options = new BackendHostOptions
        {
            ForwardedHeaders = new BackendForwardedHeadersOptions { Enabled = forwardedHeadersEnabled },
        };
        builder.Services.AddSingleton(options);

        var app = builder.Build();
        app.UseBackendForwardedHeaders(options);
        app.UseBackendCsrfGuard(options);
        app.MapPut("/guarded", () => Results.Ok());
        await app.StartAsync();
        return app;
    }
}
