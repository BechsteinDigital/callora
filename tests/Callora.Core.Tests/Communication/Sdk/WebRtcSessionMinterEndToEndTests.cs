using System;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Api.WebSocket;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// End-to-end mint → consume → resolve → TrackIncomingCall path (S4), exercised entirely
/// in-process without HTTP. Mirrors exactly what the WebRTC signalling handler does at
/// <see cref="Infrastructure.Sdk.WebRtcVoiceChannel.TrackIncomingCall"/> after a browser peer connects.
/// Uses the system clock so consume/resolve can use the same real-time "now" without TTL drift.
/// </summary>
public sealed class WebRtcSessionMinterEndToEndTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    private static (WebRtcSessionMinter Minter, WebRtcSignalingSessionStore Store, FakeWebRtcClient Client)
        BuildStack()
    {
        var client = new FakeWebRtcClient();
        var registry = new CommunicationChannelRegistry();
        var provisioner = new WebRtcChannelProvisioner(
            client,
            registry,
            "communication",
            NullLogger<WebRtcChannelProvisioner>.Instance);
        // Use system clock: the E2E flow tests routing correctness, not TTL precision.
        // TTL precision is covered by WebRtcSignalingSessionStoreTests with FakeTimeProvider.
        var store = new WebRtcSignalingSessionStore(TimeProvider.System);
        var minter = new WebRtcSessionMinter(provisioner, store, Ttl);
        return (minter, store, client);
    }

    [Fact]
    public void MintSession_ReturnsTicket_WithExpectedExpiry()
    {
        var (minter, _, _) = BuildStack();

        var ticket = minter.MintSession("ws-1", new CallTarget("sip:browser@example.com"));

        Assert.Equal(120, ticket.ExpiresInSeconds);
        Assert.False(string.IsNullOrWhiteSpace(ticket.ConnectToken));
    }

    [Fact]
    public async Task EndToEnd_Mint_Consume_Resolve_TrackIncomingCall_RaisesInboundCall()
    {
        var (minter, store, client) = BuildStack();
        var target = new CallTarget("sip:browser@example.com");

        // Step 1: consumer mints a ticket.
        var ticket = minter.MintSession("ws-1", target, callId: "call-xyz");

        // Step 2: the signalling authorizer atomically consumes the token (returns subject).
        var subject = await store.TryConsumeAsync(ticket.ConnectToken, DateTimeOffset.UtcNow, Ttl);
        Assert.NotNull(subject);

        // Step 3: the signalling handler resolves the session.
        var session = await store.ResolveAsync(subject);
        Assert.NotNull(session);
        Assert.Equal("call-xyz", session.CallId);
        Assert.Equal(target, session.Target);

        // Step 4: browser peer connects → handler calls TrackIncomingCall.
        ICall? capturedCall = null;
        session.Channel.IncomingCall += (_, e) => capturedCall = e.Call;

        var peer = session.Client.CreatePeer();
        var call = session.Channel.TrackIncomingCall(peer, session.CallId, session.Target);

        // Verify the call matches expectations.
        Assert.NotNull(capturedCall);
        Assert.Same(call, capturedCall);
        Assert.Equal(CallDirection.Inbound, call.Direction);
        Assert.Equal("call-xyz", call.CallId);
    }

    [Fact]
    public async Task SecondConsume_SameToken_ReturnsNull_SingleUse()
    {
        var (minter, store, _) = BuildStack();

        var ticket = minter.MintSession("ws-1", new CallTarget("sip:a@example.com"));
        var now = DateTimeOffset.UtcNow;

        await store.TryConsumeAsync(ticket.ConnectToken, now, Ttl);
        var second = await store.TryConsumeAsync(ticket.ConnectToken, now, Ttl);

        Assert.Null(second);
    }
}
