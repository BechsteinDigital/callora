using System;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Domain.Streaming;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Communication.Streaming;

/// <summary>
/// A media stream must not outlive the call it carries (#114). Ending a call closes its sessions —
/// so an unspent ticket stops being redeemable — and aborts the sockets already running on it.
/// </summary>
public sealed class CallEndClosesMediaStreamsTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EndingACall_ClosesItsPendingTicket()
    {
        var store = new InMemoryMediaStreamSessionStore();
        var session = Pending("sess-1", "call-1", "token-1");
        await store.AddAsync(session);

        await NewTerminator(store).CloseForCallAsync("ws-a", "call-1");

        Assert.Equal(MediaStreamSessionStatus.Closed, session.Status);
        Assert.Null(await store.TryActivateByConnectTokenAsync("token-1", Now, TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public async Task EndingACall_LeavesAnotherCallsSessionsAlone()
    {
        var store = new InMemoryMediaStreamSessionStore();
        var other = Pending("sess-2", "call-2", "token-2");
        await store.AddAsync(Pending("sess-1", "call-1", "token-1"));
        await store.AddAsync(other);

        await NewTerminator(store).CloseForCallAsync("ws-a", "call-1");

        Assert.Equal(MediaStreamSessionStatus.Pending, other.Status);
    }

    [Fact]
    public async Task EndingACall_LeavesAnotherWorkspacesSessionsAlone()
    {
        var store = new InMemoryMediaStreamSessionStore();
        var foreign = new MediaStreamSession(
            "sess-x", "call-1", "ws-b", "ai-agent", "token-x",
            AudioFormat.G711Ulaw8k20ms, MediaStreamDirection.Bidirectional, Now);
        await store.AddAsync(foreign);

        await NewTerminator(store).CloseForCallAsync("ws-a", "call-1");

        Assert.Equal(MediaStreamSessionStatus.Pending, foreign.Status);
    }

    [Fact]
    public async Task EndingACall_AbortsItsLiveSockets()
    {
        var connections = new MediaStreamConnectionRegistry();
        using var socket = new CancellationTokenSource();
        connections.Register("call-1", "sess-1", socket);

        await NewTerminator(new InMemoryMediaStreamSessionStore(), connections).CloseForCallAsync("ws-a", "call-1");

        Assert.True(socket.IsCancellationRequested);
    }

    [Fact]
    public async Task EndingACall_LeavesAnotherCallsSocketRunning()
    {
        var connections = new MediaStreamConnectionRegistry();
        using var ending = new CancellationTokenSource();
        using var unrelated = new CancellationTokenSource();
        connections.Register("call-1", "sess-1", ending);
        connections.Register("call-2", "sess-2", unrelated);

        await NewTerminator(new InMemoryMediaStreamSessionStore(), connections).CloseForCallAsync("ws-a", "call-1");

        Assert.True(ending.IsCancellationRequested);
        Assert.False(unrelated.IsCancellationRequested);
    }

    [Fact]
    public async Task ASocketThatEndedOnItsOwn_IsNoLongerAborted()
    {
        var connections = new MediaStreamConnectionRegistry();
        var socket = new CancellationTokenSource();
        var registration = connections.Register("call-1", "sess-1", socket);
        registration.Dispose();
        socket.Dispose();

        // Disposing the source after the handler returned must not turn the next hang-up into an
        // ObjectDisposedException.
        await NewTerminator(new InMemoryMediaStreamSessionStore(), connections).CloseForCallAsync("ws-a", "call-1");
    }

    [Fact]
    public async Task AFailingStore_StillLetsTheSocketsBeAborted()
    {
        // A call must finalize even when media bookkeeping fails; the live conversation is what
        // matters most, and that is the socket.
        var connections = new MediaStreamConnectionRegistry();
        using var socket = new CancellationTokenSource();
        connections.Register("call-1", "sess-1", socket);

        await NewTerminator(new ThrowingMediaStreamSessionStore(), connections).CloseForCallAsync("ws-a", "call-1");

        Assert.True(socket.IsCancellationRequested);
    }

    private static CallMediaStreamTerminator NewTerminator(
        IMediaStreamSessionStore store, MediaStreamConnectionRegistry? connections = null) =>
        new(store,
            connections ?? new MediaStreamConnectionRegistry(),
            new FakeTimeProvider(Now),
            NullLogger<CallMediaStreamTerminator>.Instance);

    private static MediaStreamSession Pending(string id, string callId, string token) =>
        new(id, callId, "ws-a", "ai-agent", token,
            AudioFormat.G711Ulaw8k20ms, MediaStreamDirection.Bidirectional, Now);
}

/// <summary>Store whose every operation fails, to prove media teardown cannot break call teardown.</summary>
internal sealed class ThrowingMediaStreamSessionStore : IMediaStreamSessionStore
{
    public Task AddAsync(MediaStreamSession session, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("unavailable");

    public Task UpdateAsync(MediaStreamSession session, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("unavailable");

    public Task<MediaStreamSession?> GetByConnectTokenAsync(string connectToken, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("unavailable");

    public Task<MediaStreamSession?> TryActivateByConnectTokenAsync(
        string connectToken, DateTimeOffset now, TimeSpan timeToLive, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("unavailable");

    public Task<MediaStreamSession?> GetAsync(string workspaceKey, string sessionId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("unavailable");

    public Task<int> CloseByCallAsync(
        string workspaceKey, string callId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("unavailable");

    public Task<int> PurgeExpiredAsync(DateTimeOffset now, TimeSpan retention, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("unavailable");

    public Task<int> DeleteByWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("unavailable");
}
