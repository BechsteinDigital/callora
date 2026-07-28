using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Api.WebSocket;
using Xunit;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// The WebRTC signalling connect-token authorizer (S3): a valid token is consumed and its resolved
/// subject flows onto the accepted connection; a missing, unknown, expired or already-used token is denied
/// fail-closed with a uniform rejection (no oracle to the caller).
/// </summary>
public sealed class WebRtcSignalingConnectTokenAuthorizerTests
{
    [Fact]
    public async Task ValidToken_Authorizes_AndResolvesSubject()
    {
        var store = new FakeTokenStore(consumedSubject: "ws-a/webrtc-1");
        var authorizer = new WebRtcSignalingConnectTokenAuthorizer(store);

        var result = await authorizer.AuthorizeAsync(Request("good-token"));

        Assert.True(result.IsAuthorized);
        Assert.Equal("ws-a/webrtc-1", result.Subject);
        Assert.Equal("good-token", store.LastToken);
    }

    [Fact]
    public async Task InvalidToken_IsDenied_FailClosed()
    {
        var store = new FakeTokenStore(consumedSubject: null); // unknown/expired/already-used
        var authorizer = new WebRtcSignalingConnectTokenAuthorizer(store);

        var result = await authorizer.AuthorizeAsync(Request("bad-token"));

        Assert.False(result.IsAuthorized);
        Assert.Null(result.Subject);
    }

    [Fact]
    public async Task MissingToken_IsDenied_WithoutTouchingStore()
    {
        var store = new FakeTokenStore(consumedSubject: "ws-a/webrtc-1");
        var authorizer = new WebRtcSignalingConnectTokenAuthorizer(store);

        var result = await authorizer.AuthorizeAsync(Request(token: null));

        Assert.False(result.IsAuthorized);
        Assert.Null(store.LastToken);
    }

    private static HostWebSocketConnectRequest Request(string? token)
    {
        var routeValues = new Dictionary<string, string>();
        if (token is not null)
        {
            routeValues[WebRtcSignalingConnectTokenAuthorizer.ConnectTokenRouteValue] = token;
        }

        return new HostWebSocketConnectRequest(
            "communication", $"webrtc/{token}", routeValues, new Dictionary<string, string[]>(), []);
    }
}

/// <summary>An <see cref="IWebRtcSignalingTokenStore"/> that returns a fixed subject and records the token.</summary>
internal sealed class FakeTokenStore(string? consumedSubject) : IWebRtcSignalingTokenStore
{
    public string? LastToken { get; private set; }

    public ValueTask<string?> TryConsumeAsync(
        string connectToken, DateTimeOffset now, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        LastToken = connectToken;
        return ValueTask.FromResult(consumedSubject);
    }
}
