using System.Text.Json;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Capabilities;
using Callora.Core.Tests.Communication.Sdk;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using Xunit;

namespace Callora.Core.Tests.Communication.Capabilities;

/// <summary>
/// Every conditional capability the manifest declares must be reachable (#115).
/// <para>
/// The manifest declared <c>communication.webrtc</c> and <c>communication.video</c> while no
/// channel published either, so the runtime source could never grant them. A dependent plugin was
/// then blocked from activating even though the service it needs was running. These tests pin the
/// declaration to the runtime: each declared capability has a publisher, and each one can be
/// granted and revoked as its channel's health moves.
/// </para>
/// </summary>
public sealed class ConditionalCapabilityContractTests
{
    private const string Workspace = "ws-a";

    [Fact]
    public void EveryDeclaredConditionalCapability_IsReachable()
    {
        var declared = ReadConditionalCapabilities();
        var reachable = new HashSet<string>(
            [
                .. WebRtcVoiceChannel.PublishedCapabilities,
                .. ConferenceChannel.PublishedCapabilities,
                // Not every capability can come from a channel: conference telephony holds only where
                // telephony and a conference are available together, which no single channel knows.
                // The source names what it derives, so a derivation is as visible here as a
                // declaration and cannot be slipped in unnoticed.
                .. CommunicationRuntimeCapabilitySource.DerivedCapabilities,
            ],
            StringComparer.Ordinal);

        // A declared capability nothing can satisfy is worse than not declaring it: a consumer waits
        // for something that will never arrive.
        Assert.All(declared, capability => Assert.Contains(capability, reachable));
    }

    [Theory]
    [InlineData(CommunicationCapabilities.Voice)]
    [InlineData(CommunicationCapabilities.WebRtc)]
    public void WebRtcChannel_GrantsAndRevokesItsCapabilities(string capability)
    {
        var registry = new CommunicationChannelRegistry();
        using var source = new CommunicationRuntimeCapabilitySource(registry);
        var channel = NewWebRtcChannel();

        using var registration = registry.Register(Workspace, channel);
        var granted = CapabilitiesOf(source);

        ReportHealth(channel, ChannelHealth.Down);
        var afterFailure = CapabilitiesOf(source);

        ReportHealth(channel, ChannelHealth.Up);
        var afterRecovery = CapabilitiesOf(source);

        Assert.Contains(capability, granted);
        Assert.DoesNotContain(capability, afterFailure);
        Assert.Contains(capability, afterRecovery);
    }

    [Fact]
    public void ConferenceChannel_GrantsAndRevokesVideo()
    {
        var registry = new CommunicationChannelRegistry();
        using var source = new CommunicationRuntimeCapabilitySource(registry);
        var channel = NewConferenceChannel();

        using var registration = registry.Register(Workspace, channel);
        var granted = CapabilitiesOf(source);

        channel.ReportHealth(ChannelHealth.Down);
        var afterFailure = CapabilitiesOf(source);

        Assert.Contains(CommunicationCapabilities.Video, granted);
        Assert.DoesNotContain(CommunicationCapabilities.Video, afterFailure);
    }

    [Fact]
    public void UnreachableDeployment_PublishesNoCapabilities()
    {
        // Without STUN/TURN or a routable bind address, NAT traversal only works for loopback
        // peers. Granting WebRTC there would tell a dependent plugin it can serve browsers.
        var registry = new CommunicationChannelRegistry();
        using var source = new CommunicationRuntimeCapabilitySource(registry);

        using var webRtc = registry.Register(Workspace, NewWebRtcChannel(externallyReachable: false));
        using var conference = registry.Register(Workspace, NewConferenceChannel(externallyReachable: false));

        var granted = CapabilitiesOf(source);

        Assert.DoesNotContain(CommunicationCapabilities.WebRtc, granted);
        Assert.DoesNotContain(CommunicationCapabilities.Video, granted);
    }

    [Fact]
    public void DeregisteringTheChannel_RevokesItsCapabilities()
    {
        var registry = new CommunicationChannelRegistry();
        using var source = new CommunicationRuntimeCapabilitySource(registry);
        var registration = registry.Register(Workspace, NewConferenceChannel());

        var granted = CapabilitiesOf(source);
        registration.Dispose();
        var afterTeardown = CapabilitiesOf(source);

        Assert.Contains(CommunicationCapabilities.Video, granted);
        Assert.Empty(afterTeardown);
    }

    /// <summary>Moves the fake channel's health the way a provider would.</summary>
    private static void ReportHealth(FakeVoiceChannel channel, ChannelHealth health)
    {
        channel.Health = health;
        channel.RaiseHealthChanged(health);
    }

    /// <summary>Capability codes currently granted, regardless of workspace.</summary>
    private static IReadOnlyList<string> CapabilitiesOf(CommunicationRuntimeCapabilitySource source) =>
        [.. source.CurrentGrants.Select(grant => grant.Capability)];

    private static ConferenceChannel NewConferenceChannel(bool externallyReachable = true) =>
        new("conference-ws-a", "Conference", "communication", externallyReachable);

    /// <summary>
    /// Stands in for the real WebRTC channel, which needs an SDK client this test does not build.
    /// It carries that channel's exact capability set and health rule, which is what the grant and
    /// revoke contract is about.
    /// </summary>
    private static FakeVoiceChannel NewWebRtcChannel(bool externallyReachable = true) =>
        new()
        {
            ChannelId = "webrtc-ws-a",
            Capabilities = WebRtcVoiceChannel.PublishedCapabilities,
            Health = externallyReachable ? ChannelHealth.Up : ChannelHealth.Degraded,
        };

    /// <summary>Reads the shipped manifest, so the test asserts against what deployment declares.</summary>
    private static IReadOnlyList<string> ReadConditionalCapabilities()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; current is not null && depth < 8; depth++)
        {
            var candidate = Path.Combine(
                current.FullName, "custom", "static-plugins", "Communication", "registry.json");
            if (File.Exists(candidate))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(candidate));
                return document.RootElement.TryGetProperty("conditionalCapabilities", out var declared)
                    ? [.. declared.EnumerateArray().Select(x => x.GetString()!)]
                    : [];
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Communication registry.json was not found from the test base directory.");
    }
}
