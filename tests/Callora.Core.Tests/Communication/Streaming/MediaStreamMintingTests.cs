using System;
using System.Threading.Tasks;
using Callora.Core.Tests.Communication.Admin;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Domain.Streaming;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Communication.Streaming;

/// <summary>
/// Minting is the gate in front of the media socket (#114). The WebSocket authorizer can only answer
/// "is this token valid" — whether the caller was ever allowed to hold it is decided here, against
/// live call tracking.
/// </summary>
public sealed class MediaStreamMintingTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MintingForAnotherWorkspacesCall_YieldsNothing()
    {
        // The call exists — just not in the workspace asking for it. Without the ownership check,
        // any operator could stream any tenant's conversation by guessing a call id.
        var callControl = new FakeCallControlService();
        callControl.LiveCalls.Add(("ws-b", "call-1"));

        var ticket = await NewMinter(callControl).MintAsync(Command("ws-a", "call-1"));

        Assert.Null(ticket);
    }

    [Fact]
    public async Task MintingForACallThatIsNoLongerLive_YieldsNothing()
    {
        var callControl = new FakeCallControlService();
        callControl.LiveCalls.Add(("ws-a", "other-call"));

        var ticket = await NewMinter(callControl).MintAsync(Command("ws-a", "call-1"));

        Assert.Null(ticket);
    }

    [Fact]
    public async Task MintingForAnOwnedLiveCall_PersistsAPendingSessionAndReturnsTheToken()
    {
        var store = new InMemoryMediaStreamSessionStore();
        var ticket = await NewMinter(LiveCall(), store).MintAsync(Command("ws-a", "call-1"));

        Assert.NotNull(ticket);
        var session = await store.GetAsync("ws-a", ticket!.SessionId);
        Assert.NotNull(session);
        Assert.Equal(MediaStreamSessionStatus.Pending, session!.Status);
        Assert.Equal("call-1", session.CallId);
        Assert.Equal("ai-agent", session.ConsumerRef);
    }

    [Fact]
    public async Task ThePersistedSessionKeepsOnlyTheTokenHash()
    {
        // A leaked database row must not be a working ticket (#108).
        var store = new InMemoryMediaStreamSessionStore();
        var ticket = await NewMinter(LiveCall(), store).MintAsync(Command("ws-a", "call-1"));

        var session = await store.GetAsync("ws-a", ticket!.SessionId);
        Assert.NotEqual(ticket.ConnectToken, session!.ConnectTokenHash);
        Assert.Equal(MediaStreamSession.HashToken(ticket.ConnectToken), session.ConnectTokenHash);
    }

    [Fact]
    public async Task EveryMintProducesAFreshToken()
    {
        var store = new InMemoryMediaStreamSessionStore();
        var minter = NewMinter(LiveCall(), store);

        var first = await minter.MintAsync(Command("ws-a", "call-1"));
        var second = await minter.MintAsync(Command("ws-a", "call-1"));

        Assert.NotEqual(first!.ConnectToken, second!.ConnectToken);
        Assert.NotEqual(first.SessionId, second.SessionId);
    }

    [Fact]
    public async Task AMintedTokenIsRedeemableExactlyOnce()
    {
        var store = new InMemoryMediaStreamSessionStore();
        var ticket = await NewMinter(LiveCall(), store).MintAsync(Command("ws-a", "call-1"));

        var first = await store.TryActivateByConnectTokenAsync(ticket!.ConnectToken, Now, TimeSpan.FromMinutes(2));
        var replay = await store.TryActivateByConnectTokenAsync(ticket.ConnectToken, Now, TimeSpan.FromMinutes(2));

        Assert.NotNull(first);
        Assert.Null(replay);
    }

    [Fact]
    public async Task AMintedTokenStopsWorkingAfterItsWindow()
    {
        var store = new InMemoryMediaStreamSessionStore();
        var ticket = await NewMinter(LiveCall(), store).MintAsync(Command("ws-a", "call-1"));

        var late = await store.TryActivateByConnectTokenAsync(
            ticket!.ConnectToken, Now.AddMinutes(3), TimeSpan.FromMinutes(2));

        Assert.Null(late);
    }

    [Theory]
    [InlineData(MediaStreamDirection.Inbound)]
    [InlineData(MediaStreamDirection.Outbound)]
    [InlineData(MediaStreamDirection.Bidirectional)]
    public async Task TheRequestedDirectionIsWhatGetsPersisted(MediaStreamDirection direction)
    {
        // The socket enforces what the session says, so a widened direction here would silently
        // hand out more access than was asked for.
        var store = new InMemoryMediaStreamSessionStore();
        var ticket = await NewMinter(LiveCall(), store)
            .MintAsync(new MintMediaStreamCommand("ws-a", "call-1", "ai-agent", direction));

        var session = await store.GetAsync("ws-a", ticket!.SessionId);
        Assert.Equal(direction, session!.Direction);
        Assert.Equal(direction, ticket.Direction);
    }

    [Fact]
    public async Task TheTicketsTextRepresentationOmitsTheToken()
    {
        // A log statement interpolating the ticket must not leak a live credential.
        var ticket = await NewMinter(LiveCall()).MintAsync(Command("ws-a", "call-1"));

        Assert.DoesNotContain(ticket!.ConnectToken, ticket.ToString(), StringComparison.Ordinal);
        Assert.Contains(ticket.SessionId, ticket.ToString(), StringComparison.Ordinal);
    }

    private static FakeCallControlService LiveCall()
    {
        var callControl = new FakeCallControlService();
        callControl.LiveCalls.Add(("ws-a", "call-1"));
        return callControl;
    }

    private static MediaStreamSessionMinter NewMinter(
        ICallControlService callControl, IMediaStreamSessionStore? store = null) =>
        new(callControl, store ?? new InMemoryMediaStreamSessionStore(), new FakeTimeProvider(Now));

    private static MintMediaStreamCommand Command(string workspaceKey, string callId) =>
        new(workspaceKey, callId, "ai-agent", MediaStreamDirection.Bidirectional);
}
