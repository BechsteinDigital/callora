using System.Net;
using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Application.Webhooks;
using Xunit;

namespace Callora.Host.Backend.Tests.Application.Webhooks;

public sealed class WebhookEgressGuardTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("192.168.1.1", true)]
    [InlineData("169.254.10.10", true)]
    [InlineData("224.0.0.1", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("203.0.113.10", false)]
    public void IsForbidden_ClassifiesIPv4Ranges(string address, bool expected)
    {
        Assert.Equal(expected, WebhookEgressGuard.IsForbidden(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("::1", true)]
    [InlineData("fe80::1", true)]
    [InlineData("fc00::1", true)]
    [InlineData("fd12:3456::1", true)]
    [InlineData("2001:4860:4860::8888", false)]
    public void IsForbidden_ClassifiesIPv6Ranges(string address, bool expected)
    {
        Assert.Equal(expected, WebhookEgressGuard.IsForbidden(IPAddress.Parse(address)));
    }

    [Fact]
    public async Task Guard_BlocksLoopbackLiteral_AndAllowsInDevMode()
    {
        var strict = new WebhookEgressGuard(new BackendHostOptions());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strict.EnsureAllowedAsync(new Uri("http://127.0.0.1:5000/hook")));

        var dev = new WebhookEgressGuard(new BackendHostOptions { AllowPrivateWebhookTargets = true });
        await dev.EnsureAllowedAsync(new Uri("http://127.0.0.1:5000/hook"));
    }
}
