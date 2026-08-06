using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Calls;
using Xunit;

namespace Callora.Core.Tests.Communication.Calls;

/// <summary>
/// Who decides what happens to an inbound call. Today every consumer that subscribes to a channel
/// receives it and whoever calls accept first wins — which makes a wallboard one careless line away
/// from answering somebody's customer.
/// </summary>
public sealed class IncomingCallOwnershipTests
{
    private const string Workspace = "ws-a";

    [Fact]
    public async Task AnOwnerThatClaimsTheCall_GetsIt()
    {
        var owner = new RecordingOwner(claims: true);
        var owners = new IncomingCallOwnership([owner]);
        var call = new ControllableCall("call-1", CallState.Ringing, CallDirection.Inbound);

        var claimed = await owners.OfferAsync(Workspace, call);

        Assert.True(claimed);
        Assert.Same(call, owner.Offered);
    }

    [Fact]
    public async Task TheFirstOwnerThatClaims_EndsTheOffer()
    {
        var first = new RecordingOwner(claims: true);
        var second = new RecordingOwner(claims: true);
        var owners = new IncomingCallOwnership([first, second]);

        await owners.OfferAsync(Workspace, new ControllableCall("call-1", CallState.Ringing, CallDirection.Inbound));

        // Offering it onwards after somebody took it would hand the same call to two owners, and the
        // second would act on a call that is already being answered.
        Assert.NotNull(first.Offered);
        Assert.Null(second.Offered);
    }

    [Fact]
    public async Task AnOwnerThatDeclines_PassesItOn()
    {
        var declining = new RecordingOwner(claims: false);
        var claiming = new RecordingOwner(claims: true);
        var owners = new IncomingCallOwnership([declining, claiming]);

        var claimed = await owners.OfferAsync(Workspace, new ControllableCall("call-1", CallState.Ringing, CallDirection.Inbound));

        Assert.True(claimed);
        Assert.NotNull(declining.Offered);
        Assert.NotNull(claiming.Offered);
    }

    [Fact]
    public async Task WithNoOwnerAtAll_TheCallIsNotClaimed()
    {
        var owners = new IncomingCallOwnership([]);

        Assert.False(await owners.OfferAsync(Workspace, new ControllableCall("call-1", CallState.Ringing, CallDirection.Inbound)));
    }

    [Fact]
    public async Task AnOwnerThatThrows_DoesNotSwallowTheCall()
    {
        var throwing = new ThrowingOwner();
        var claiming = new RecordingOwner(claims: true);
        var owners = new IncomingCallOwnership([throwing, claiming]);

        // One broken consumer must not make the whole trunk unreachable — the call moves on.
        Assert.True(await owners.OfferAsync(Workspace, new ControllableCall("call-1", CallState.Ringing, CallDirection.Inbound)));
        Assert.NotNull(claiming.Offered);
    }
}

/// <summary>An owner that records what it was offered and either claims it or does not.</summary>
internal sealed class RecordingOwner(bool claims) : IIncomingCallOwner
{
    public ICall? Offered { get; private set; }

    public Task<bool> TryClaimAsync(string workspaceKey, ICall call, CancellationToken cancellationToken = default)
    {
        Offered = call;
        return Task.FromResult(claims);
    }
}

/// <summary>An owner that fails, to prove a broken consumer does not strand the call.</summary>
internal sealed class ThrowingOwner : IIncomingCallOwner
{
    public Task<bool> TryClaimAsync(string workspaceKey, ICall call, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("owner is broken");
}
