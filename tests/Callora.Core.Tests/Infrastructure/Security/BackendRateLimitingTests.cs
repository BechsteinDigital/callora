using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;
using ForwardedHeaderKinds = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders;

namespace Callora.Core.Tests.Infrastructure.Security;

public sealed class BackendRateLimitingTests
{
    private const string TrustedProxy = "172.20.0.5";

    [Fact]
    public void ResolveClientKey_IgnoresSpoofedForwardedHeader()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.7, 10.0.0.1";

        // The raw header is attacker-controlled and must not shift the partition.
        Assert.Equal("192.0.2.10", BackendRateLimiting.ResolveClientKey(context));
    }

    [Fact]
    public void ResolveClientKey_UsesRemoteAddress()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");

        Assert.Equal("192.0.2.10", BackendRateLimiting.ResolveClientKey(context));
    }

    [Fact]
    public void ResolveClientKey_WithoutAnyAddress_ReturnsUnknown()
    {
        Assert.Equal("unknown", BackendRateLimiting.ResolveClientKey(new DefaultHttpContext()));
    }

    [Fact]
    public async Task SpoofedForwardedFor_FromUntrustedPeer_DoesNotCreateFreshBuckets()
    {
        // The caller is not the configured proxy, so its header is ignored.
        await using var app = await CreateAppAsync(
            new BackendForwardedHeadersOptions { Enabled = true, KnownProxies = [TrustedProxy] },
            peerAddress: "198.51.100.44");
        var client = app.GetTestClient();

        var first = await SendAsync(client, "203.0.113.1");
        var second = await SendAsync(client, "203.0.113.2");

        Assert.Equal("198.51.100.44", first);
        Assert.Equal("198.51.100.44", second);
    }

    [Fact]
    public async Task ForwardedFor_IsIgnored_WhenNoTrustedProxyIsConfigured()
    {
        await using var app = await CreateAppAsync(
            new BackendForwardedHeadersOptions { Enabled = true },
            peerAddress: TrustedProxy);
        var client = app.GetTestClient();

        var first = await SendAsync(client, "203.0.113.1");
        var second = await SendAsync(client, "203.0.113.2");

        Assert.Equal(TrustedProxy, first);
        Assert.Equal(TrustedProxy, second);
    }

    [Fact]
    public async Task TrustedProxy_ForwardsTheRealClientAddress()
    {
        await using var app = await CreateAppAsync(
            new BackendForwardedHeadersOptions { Enabled = true, KnownProxies = [TrustedProxy] },
            peerAddress: TrustedProxy);
        var client = app.GetTestClient();

        var first = await SendAsync(client, "203.0.113.1");
        var second = await SendAsync(client, "203.0.113.2");

        Assert.Equal("203.0.113.1", first);
        Assert.Equal("203.0.113.2", second);
    }

    [Fact]
    public void Build_WithoutExplicitTrust_DoesNotProcessForwardedFor()
    {
        var built = BackendForwardedHeaders.Build(new BackendForwardedHeadersOptions { Enabled = true });

        Assert.False(built.ForwardedHeaders.HasFlag(ForwardedHeaderKinds.XForwardedFor));
        Assert.True(built.ForwardedHeaders.HasFlag(ForwardedHeaderKinds.XForwardedProto));
    }

    [Fact]
    public void Build_WithExplicitTrust_ProcessesForwardedFor()
    {
        var built = BackendForwardedHeaders.Build(new BackendForwardedHeadersOptions
        {
            Enabled = true,
            KnownNetworks = ["172.16.0.0/12"]
        });

        Assert.True(built.ForwardedHeaders.HasFlag(ForwardedHeaderKinds.XForwardedFor));
        Assert.Single(built.KnownIPNetworks);
    }

    private static Task<string> SendAsync(HttpClient client, string forwardedFor)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/client-key");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        return client.SendAsync(request).ContinueWith(
            task => task.Result.Content.ReadAsStringAsync().Result,
            TaskScheduler.Default);
    }

    private static async Task<WebApplication> CreateAppAsync(
        BackendForwardedHeadersOptions forwardedHeaders,
        string peerAddress)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var app = builder.Build();

        // TestServer leaves the peer address unset; stamp it so trust evaluation
        // sees a realistic connection.
        var peer = IPAddress.Parse(peerAddress);
        app.Use(async (context, next) =>
        {
            context.Connection.RemoteIpAddress = peer;
            await next();
        });
        app.UseBackendForwardedHeaders(new BackendHostOptions { ForwardedHeaders = forwardedHeaders });
        app.MapGet("/client-key", (HttpContext context) => BackendRateLimiting.ResolveClientKey(context));
        await app.StartAsync();
        return app;
    }
}
