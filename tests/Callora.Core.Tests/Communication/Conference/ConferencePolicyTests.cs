using System;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions.Conference;
using Callora.Plugin.Communication.Application.Conference;
using Callora.Plugin.Communication.Application.RealtimeMedia;
using Xunit;

namespace Callora.Core.Tests.Communication.Conference;

/// <summary>
/// The per-conference policy a vertical sets when it opens a room. Its point is the rooms where a
/// telephone must not be able to join at all — a medical consultation, say — as opposed to the rooms
/// where dial-in is the feature.
/// </summary>
public sealed class ConferencePolicyTests
{
    private const string Conf = "conf-1";

    [Fact]
    public async Task Join_WithoutAPolicy_LeavesTheConferenceUnrestricted()
    {
        var service = NewService();

        await service.JoinAsync(Conf, "alice");

        Assert.False(service.GetPolicy(Conf).RequiresEndToEndEncryption);
    }

    [Fact]
    public async Task Join_WithAPolicy_AppliesItToTheConference()
    {
        var service = NewService();

        await service.JoinAsync(Conf, "alice", new ConferencePolicy(RequiresEndToEndEncryption: true));

        Assert.True(service.GetPolicy(Conf).RequiresEndToEndEncryption);
    }

    [Fact]
    public async Task ALaterJoinWithoutAPolicy_DoesNotRelaxTheOneInForce()
    {
        var service = NewService();
        await service.JoinAsync(Conf, "alice", new ConferencePolicy(RequiresEndToEndEncryption: true));

        await service.JoinAsync(Conf, "bob");

        // Omitting the policy must not weaken the room. Otherwise anyone able to join could strip the
        // restriction by simply not restating it, and the next dial-in would be let through.
        Assert.True(service.GetPolicy(Conf).RequiresEndToEndEncryption);
    }

    [Fact]
    public async Task ALaterJoinContradictingThePolicy_IsRejected()
    {
        var service = NewService();
        await service.JoinAsync(Conf, "alice", new ConferencePolicy(RequiresEndToEndEncryption: true));

        // Silently keeping the stricter policy would leave the caller believing it opened an
        // unrestricted room; silently taking the looser one would break the first participant's
        // expectation. Both readings are wrong, so the disagreement is reported.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.JoinAsync(Conf, "bob", new ConferencePolicy(RequiresEndToEndEncryption: false)));
    }

    [Fact]
    public void GetPolicy_ForAnUnknownConference_IsUnrestricted()
    {
        Assert.False(NewService().GetPolicy("never-opened").RequiresEndToEndEncryption);
    }

    private static ConferenceService NewService() =>
        new(new FakeRealtimeMediaProvider(), new MediaPeerOptions { EnableVideo = true });
}
