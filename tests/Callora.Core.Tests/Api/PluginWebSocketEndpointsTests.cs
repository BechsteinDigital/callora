using Callora.Administration.Api;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace Callora.Core.Tests.Api;

/// <summary>
/// Host-WebSocket walking skeleton: the reserved <c>/ws/{pluginId}/…</c> prefix
/// upgrades to a plugin socket only after the route's <see cref="IWebSocketConnectAuthorizer"/>
/// authorizes the connect. Covers the authorized round-trip (route values + resolved
/// subject flow through), the pre-accept rejection of an invalid connect-token, an
/// unknown route, and a non-upgrade GET.
/// </summary>
public sealed class PluginWebSocketEndpointsTests
{
    [Fact]
    public async Task Connect_WithValidToken_UpgradesAndEchoesWithRouteContext()
    {
        await using var app = await CreateAppAsync();
        var wsClient = app.GetTestServer().CreateWebSocketClient();

        var socket = await wsClient.ConnectAsync(
            new Uri("ws://localhost/ws/echo-plugin/echo/session-1?token=good"),
            CancellationToken.None);

        await socket.SendAsync(Encoding.UTF8.GetBytes("ping"), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
        var reply = await ReceiveTextAsync(socket);

        // subject|sessionId|received — proves authorizer subject + route value + duplex payload.
        Assert.Equal("ws:good|session-1|ping", reply);
    }

    [Fact]
    public async Task Connect_WithInvalidToken_IsRejectedWith401BeforeUpgrade()
    {
        await using var app = await CreateAppAsync();
        var wsClient = app.GetTestServer().CreateWebSocketClient();

        // The handshake never reaches 101 — the authorizer's 401 aborts before AcceptWebSocketAsync.
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            wsClient.ConnectAsync(
                new Uri("ws://localhost/ws/echo-plugin/echo/session-1?token=bad"),
                CancellationToken.None));

        Assert.Contains("401", error.Message);
    }

    [Fact]
    public async Task Connect_ToUnknownRoute_IsRejectedWith404()
    {
        await using var app = await CreateAppAsync();
        var wsClient = app.GetTestServer().CreateWebSocketClient();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            wsClient.ConnectAsync(
                new Uri("ws://localhost/ws/echo-plugin/does-not-exist?token=good"),
                CancellationToken.None));

        Assert.Contains("404", error.Message);
    }

    [Fact]
    public async Task Get_WithoutUpgrade_ReturnsBadRequest()
    {
        await using var app = await CreateAppAsync();
        var httpClient = app.GetTestClient();

        var response = await httpClient.GetAsync("/ws/echo-plugin/echo/session-1?token=good");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<string> ReceiveTextAsync(WebSocket socket)
    {
        var buffer = new byte[1024];
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var contributor = new EchoWebSocketContributor
        {
            PluginId = "echo-plugin",
            WebSocketRoutes =
            [
                new HostWebSocketRouteRegistration(
                    "echo/{sessionId}",
                    new TokenConnectAuthorizer(),
                    new EchoWebSocketHandler())
            ]
        };

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ICalloraPluginCatalog>(new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IHostWebSocketEndpointContributor)] = [contributor]
        }));

        var app = builder.Build();
        app.UseWebSockets();
        app.MapPluginWebSocketEndpoints();
        await app.StartAsync();
        return app;
    }
}

internal sealed class EchoWebSocketContributor : IHostWebSocketEndpointContributor
{
    public required string PluginId { get; init; }

    public required IReadOnlyList<HostWebSocketRouteRegistration> WebSocketRoutes { get; init; }
}

/// <summary>Authorizes only when the <c>token</c> query equals "good", carrying it as the subject.</summary>
internal sealed class TokenConnectAuthorizer : IWebSocketConnectAuthorizer
{
    public ValueTask<WebSocketConnectAuthorization> AuthorizeAsync(
        HostWebSocketConnectRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = request.Query.TryGetValue("token", out var values) && values.Length > 0
            ? values[0]
            : null;

        return ValueTask.FromResult(token == "good"
            ? WebSocketConnectAuthorization.Allow($"ws:{token}")
            : WebSocketConnectAuthorization.Deny("invalid token"));
    }
}

/// <summary>Echoes one text frame back as <c>subject|sessionId|payload</c>, then closes.</summary>
internal sealed class EchoWebSocketHandler : IHostWebSocketHandler
{
    public async Task HandleAsync(HostWebSocketConnection connection, CancellationToken cancellationToken = default)
    {
        var socket = connection.Socket;
        var buffer = new byte[1024];
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
        var received = Encoding.UTF8.GetString(buffer, 0, result.Count);

        var sessionId = connection.Request.RouteValues.TryGetValue("sessionId", out var value) ? value : string.Empty;
        var reply = Encoding.UTF8.GetBytes($"{connection.Subject}|{sessionId}|{received}");

        await socket.SendAsync(new ArraySegment<byte>(reply), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", cancellationToken);
    }
}
