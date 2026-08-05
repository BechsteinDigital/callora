using Callora.Core.Tests.Communication.Sdk;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Admin;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Xunit;

namespace Callora.Core.Tests.Communication.Admin;

/// <summary>
/// A drain has to be distinguishable from an outage (ADR-018 §2.1). Both stop new calls, but only
/// one of them is a problem, and an operator watching a deployment should not be paged for the
/// planned one.
/// </summary>
public sealed class CommunicationReadinessDrainTests
{
    [Fact]
    public async Task AHealthyPluginIsReadyUntilItIsToldToDrain()
    {
        var probe = ProbeWith(ChannelHealth.Up);

        Assert.Equal(CommunicationReadiness.Ready, (await probe.ProbeAsync(TestToken)).Status);

        probe.MarkDraining();

        Assert.Equal(CommunicationReadiness.Draining, (await probe.ProbeAsync(TestToken)).Status);
    }

    [Fact]
    public async Task DrainingOutranksTheChannelsItJustTookDown()
    {
        // This is why the verdict is a separate value rather than a folded dependency: quiescing a
        // line makes it report down, and "unavailable" would tell an operator something broke.
        var probe = ProbeWith(ChannelHealth.Down);
        probe.MarkDraining();

        var status = await probe.ProbeAsync(TestToken);

        Assert.Equal(CommunicationReadiness.Draining, status.Status);

        // The dependency detail is still reported honestly — only the headline changes.
        Assert.Contains(status.Dependencies, dependency => dependency.Name == "channels" && dependency.State == "down");
    }

    [Fact]
    public async Task MarkingTwiceChangesNothing()
    {
        var probe = ProbeWith(ChannelHealth.Up);
        probe.MarkDraining();
        probe.MarkDraining();

        Assert.Equal(CommunicationReadiness.Draining, (await probe.ProbeAsync(TestToken)).Status);
    }

    private static CancellationToken TestToken => CancellationToken.None;

    private static CommunicationReadinessProbe ProbeWith(ChannelHealth health)
    {
        var registry = new CommunicationChannelRegistry();
        registry.Register("ws-a", new FakeVoiceChannel { ChannelId = "ch-1", Health = health });
        return new CommunicationReadinessProbe(registry, accountStore: null, webRtcConfigured: true);
    }
}
