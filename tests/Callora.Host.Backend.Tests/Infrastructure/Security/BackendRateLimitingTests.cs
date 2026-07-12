using Callora.Host.Backend.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Callora.Host.Backend.Tests.Infrastructure.Security;

public sealed class BackendRateLimitingTests
{
    [Fact]
    public void ResolveClientKey_PrefersFirstForwardedAddress()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.7, 10.0.0.1";

        Assert.Equal("203.0.113.7", BackendRateLimiting.ResolveClientKey(context));
    }

    [Fact]
    public void ResolveClientKey_FallsBackToRemoteAddress()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.10");

        Assert.Equal("192.0.2.10", BackendRateLimiting.ResolveClientKey(context));
    }

    [Fact]
    public void ResolveClientKey_WithoutAnyAddress_ReturnsUnknown()
    {
        Assert.Equal("unknown", BackendRateLimiting.ResolveClientKey(new DefaultHttpContext()));
    }
}
