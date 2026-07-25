using System;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Accounts;
using Callora.Plugin.Communication.Infrastructure.Capabilities;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using BridgeAudioFormat = CalloraVoipSdk.BridgeAudioFormat;
using FakePluginDataProtector = Callora.Core.Tests.Communication.Sdk.FakePluginDataProtector;
using SdkSipTransport = CalloraVoipSdk.SipTransport;
using VoipClient = CalloraVoipSdk.VoipClient;
using VoipConfiguration = CalloraVoipSdk.VoipConfiguration;

namespace Callora.Core.Tests.Communication.Integration;

/// <summary>
/// B4-deep-3 closes the runtime-capability loop against a real Asterisk (opt-in via
/// CALLORA_ASTERISK_TESTS=1): the real production seam — <see cref="VoipClientVoiceRuntime"/> over a live
/// <see cref="VoipClient"/>, driven by <see cref="SdkVoiceChannelConnector"/> — registers a persisted
/// account, the resulting <see cref="SdkVoiceChannel"/> reports <see cref="ChannelHealth.Up"/>, and once
/// it is in the channel registry the <see cref="CommunicationRuntimeCapabilitySource"/> actually grants
/// <c>communication.voice</c> for that workspace. The fake-backed unit tests prove each link in isolation;
/// this proves the whole chain over real SIP. Start Asterisk first (see ops/spikes/asterisk-b4deep3).
/// </summary>
[Trait("Category", "Asterisk")]
public sealed class AsteriskRuntimeCapabilityIntegrationTests
{
    private const string PluginId = "communication";

    private static bool Enabled => Environment.GetEnvironmentVariable("CALLORA_ASTERISK_TESTS") == "1";

    [SkippableFact]
    public async Task RealRegistration_GrantsCommunicationVoice_ThroughFullChain()
    {
        Skip.IfNot(Enabled, "Set CALLORA_ASTERISK_TESTS=1 with a running Asterisk to run this.");

        using var client = new VoipClient(new VoipConfiguration
        {
            DefaultTransport = SdkSipTransport.Udp,
            PreferredAudioCodecs = ["PCMU"],
            BridgeAudioFormat = BridgeAudioFormat.Pcmu,
        });

        // The exact production wiring: real SDK runtime → account factory → connector.
        var connector = new SdkVoiceChannelConnector(
            new SdkSipAccountFactory(new FakePluginDataProtector(("pw-ref", "callora")), PluginId),
            new VoipClientVoiceRuntime(client),
            PluginId,
            NullLogger<SdkVoiceChannelConnector>.Instance);

        var auth = new DigestAuthentication("callora", authId: null, passwordSecretRef: "pw-ref");
        var connection = new SipConnection(
            "127.0.0.1", 5060, SipTransport.Udp, SipAccountMode.Register, auth, registrationExpirySeconds: 600);
        var account = new SipAccount(
            "acc-asterisk", "ws-asterisk", "Asterisk Line", connection, maxConcurrentCalls: 1, enabled: true);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var channel = await connector.ConnectAsync(account, cts.Token);

        Assert.NotNull(channel);
        // IVoiceChannel does not extend IDisposable; the concrete SdkVoiceChannel does (it holds line
        // event subscriptions), so dispose it explicitly once the assertions are done.
        try
        {
            Assert.Equal(ChannelHealth.Up, channel!.Health); // real registration → line Registered → channel Up

            // The registry + source are the real host-side consumers: a healthy voice channel grants voice.
            var registry = new CommunicationChannelRegistry();
            using (registry.Register(account.WorkspaceKey, channel))
            using (var source = new CommunicationRuntimeCapabilitySource(registry))
            {
                Assert.Equal(
                    [new RuntimeCapabilityGrant(CommunicationCapabilities.Voice, account.WorkspaceKey)],
                    source.CurrentGrants);
            }
        }
        finally
        {
            (channel as IDisposable)?.Dispose();
        }
    }
}
