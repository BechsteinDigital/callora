using System;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Api.WebSocket;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// The in-memory WebRTC signalling session store (S4): single-use token semantics, TTL expiry,
/// one-shot resolve, and fail-closed on unknown tokens.
/// </summary>
public sealed class WebRtcSignalingSessionStoreTests
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(2);

    private static WebRtcSignalingSession MakeSession()
    {
        var client = new FakeWebRtcClient();
        var channel = new WebRtcVoiceChannel("webrtc-ws", "WebRTC", "communication", client);
        return new WebRtcSignalingSession(client, channel, "call-1", new CallTarget("sip:browser@example.com"));
    }

    [Fact]
    public async Task Mint_ThenTryConsume_ReturnsToken_AsSubject()
    {
        var clock = new FakeTimeProvider();
        var store = new WebRtcSignalingSessionStore(clock, DefaultTtl);
        var session = MakeSession();

        var token = store.Mint(session);

        var subject = await store.TryConsumeAsync(token, clock.GetUtcNow(), DefaultTtl);

        Assert.Equal(token, subject);
    }

    [Fact]
    public async Task TryConsume_SecondTime_SameToken_ReturnsNull()
    {
        var clock = new FakeTimeProvider();
        var store = new WebRtcSignalingSessionStore(clock, DefaultTtl);
        var token = store.Mint(MakeSession());
        var now = clock.GetUtcNow();

        await store.TryConsumeAsync(token, now, DefaultTtl);
        var second = await store.TryConsumeAsync(token, now, DefaultTtl);

        Assert.Null(second);
    }

    [Fact]
    public async Task TryConsume_ExpiredToken_ReturnsNull()
    {
        var clock = new FakeTimeProvider();
        var store = new WebRtcSignalingSessionStore(clock, DefaultTtl);
        var token = store.Mint(MakeSession());

        // Advance past the TTL.
        clock.Advance(DefaultTtl + TimeSpan.FromSeconds(1));

        var subject = await store.TryConsumeAsync(token, clock.GetUtcNow(), DefaultTtl);

        Assert.Null(subject);
    }

    [Fact]
    public async Task ResolveAsync_AfterConsume_ReturnsSession()
    {
        var clock = new FakeTimeProvider();
        var store = new WebRtcSignalingSessionStore(clock, DefaultTtl);
        var session = MakeSession();
        var token = store.Mint(session);

        var subject = await store.TryConsumeAsync(token, clock.GetUtcNow(), DefaultTtl);
        var resolved = await store.ResolveAsync(subject);

        Assert.Same(session, resolved);
    }

    [Fact]
    public async Task ResolveAsync_SecondTime_ReturnsNull_OneShot()
    {
        var clock = new FakeTimeProvider();
        var store = new WebRtcSignalingSessionStore(clock, DefaultTtl);
        var token = store.Mint(MakeSession());

        var subject = await store.TryConsumeAsync(token, clock.GetUtcNow(), DefaultTtl);
        await store.ResolveAsync(subject);
        var second = await store.ResolveAsync(subject);

        Assert.Null(second);
    }

    [Fact]
    public async Task TryConsume_UnknownToken_ReturnsNull()
    {
        var clock = new FakeTimeProvider();
        var store = new WebRtcSignalingSessionStore(clock, DefaultTtl);

        var subject = await store.TryConsumeAsync("does-not-exist", clock.GetUtcNow(), DefaultTtl);

        Assert.Null(subject);
    }

    [Fact]
    public async Task ResolveAsync_UnknownSubject_ReturnsNull()
    {
        var clock = new FakeTimeProvider();
        var store = new WebRtcSignalingSessionStore(clock, DefaultTtl);

        var session = await store.ResolveAsync("ghost-subject");

        Assert.Null(session);
    }

    [Fact]
    public async Task ResolveAsync_NullSubject_ReturnsNull()
    {
        var clock = new FakeTimeProvider();
        var store = new WebRtcSignalingSessionStore(clock, DefaultTtl);

        var session = await store.ResolveAsync(null);

        Assert.Null(session);
    }

    [Fact]
    public async Task Mint_PurgesExpiredEntries_BeforeInserting()
    {
        // Arrange: mint a first token, then advance time past the lifetime so it is expired.
        var clock = new FakeTimeProvider();
        var store = new WebRtcSignalingSessionStore(clock, DefaultTtl);
        var firstToken = store.Mint(MakeSession());

        clock.Advance(DefaultTtl + TimeSpan.FromSeconds(1));

        // Act: minting a second token should purge the first (expired) entry.
        store.Mint(MakeSession());

        // Assert: the first (expired) token is no longer resolvable.
        var resolved = await store.ResolveAsync(firstToken);
        Assert.Null(resolved);
    }
}
