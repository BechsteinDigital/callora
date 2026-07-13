using System.Net;
using System.Net.Sockets;
using System.Text;
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

    [Fact]
    public async Task ConnectCallback_BlocksPrivateAddressAtConnectTime()
    {
        var guard = new WebhookEgressGuard(new BackendHostOptions());
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = guard.ConnectAsync
        };
        using var client = new HttpClient(handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAsync(new Uri("http://127.0.0.1:59999/hook")));
        Assert.Contains("blocked", exception.Message + exception.InnerException?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectCallback_InDevMode_ConnectsToLocalTarget()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = Task.Run(async () =>
        {
            using var socket = await listener.AcceptSocketAsync();
            var buffer = new byte[4096];
            await socket.ReceiveAsync(buffer);
            await socket.SendAsync(Encoding.ASCII.GetBytes(
                "HTTP/1.1 204 No Content\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));
        });

        try
        {
            var guard = new WebhookEgressGuard(new BackendHostOptions { AllowPrivateWebhookTargets = true });
            using var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectCallback = guard.ConnectAsync
            };
            using var client = new HttpClient(handler);

            var response = await client.GetAsync(new Uri($"http://127.0.0.1:{port}/hook"));
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            await serverTask;
        }
        finally
        {
            listener.Stop();
        }
    }
}
