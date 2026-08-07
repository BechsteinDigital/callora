using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Calls;
using Xunit;

namespace Callora.Core.Tests.Communication.Calls;

/// <summary>
/// Where consumers sign up to decide about inbound calls, and how a call is offered around.
/// </summary>
public sealed class IncomingCallOwnerRegistryTests
{
    private const string Workspace = "ws-a";

    [Fact]
    public async Task ACallIsOfferedToTheWorkspacesOwners()
    {
        var registry = new IncomingCallOwnerRegistry();
        var owner = new RecordingOwner(claims: true);
        registry.Register(Workspace, owner);

        var claimed = await registry.OfferAsync(Workspace, NewCall());

        Assert.True(claimed);
        Assert.NotNull(owner.Offered);
    }

    [Fact]
    public async Task AnOwnerOfAnotherWorkspace_IsNotOffered()
    {
        var registry = new IncomingCallOwnerRegistry();
        var foreign = new RecordingOwner(claims: true);
        registry.Register("ws-other", foreign);

        // A trunk's calls belong to the workspace that owns it; offering them elsewhere would let one
        // tenant answer another's customer.
        Assert.False(await registry.OfferAsync(Workspace, NewCall()));
        Assert.Null(foreign.Offered);
    }

    [Fact]
    public async Task ADisposedRegistration_StopsReceivingCalls()
    {
        var registry = new IncomingCallOwnerRegistry();
        var owner = new RecordingOwner(claims: true);
        var registration = registry.Register(Workspace, owner);

        registration.Dispose();

        // A deactivated consumer must take its claim with it, or calls are offered to something that
        // is no longer there and nobody answers.
        Assert.False(await registry.OfferAsync(Workspace, NewCall()));
        Assert.Null(owner.Offered);
    }

    [Fact]
    public async Task OwnersAreOfferedInRegistrationOrder()
    {
        var registry = new IncomingCallOwnerRegistry();
        var first = new RecordingOwner(claims: false);
        var second = new RecordingOwner(claims: true);
        registry.Register(Workspace, first);
        registry.Register(Workspace, second);

        Assert.True(await registry.OfferAsync(Workspace, NewCall()));
        Assert.NotNull(first.Offered);
        Assert.NotNull(second.Offered);
    }

    [Fact]
    public void WithNobodySignedUp_TheRegistryKnowsIt()
    {
        var registry = new IncomingCallOwnerRegistry();

        // The distinction the inbound path needs: nobody signed up at all is not the same as everybody
        // declining. Without owners the old behaviour has to stand, or every call would be refused the
        // moment this shipped.
        Assert.False(registry.HasOwners(Workspace));
        registry.Register(Workspace, new RecordingOwner(claims: false));
        Assert.True(registry.HasOwners(Workspace));
    }

    private static ICall NewCall() => new ControllableCall("call-1", CallState.Ringing, CallDirection.Inbound);
}
