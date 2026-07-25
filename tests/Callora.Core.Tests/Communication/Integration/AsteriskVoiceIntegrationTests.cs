using System;
using System.Threading;
using System.Threading.Tasks;
using CalloraVoipSdk;
using Xunit;
using CoreSipTransport = CalloraVoipSdk.Core.Domain.Lines.SipTransport;
using LineState = CalloraVoipSdk.Core.Domain.Lines.LineState;
using SipAccount = CalloraVoipSdk.Core.Domain.Lines.SipAccount;

namespace Callora.Core.Tests.Communication.Integration;

/// <summary>
/// B4-deep-3 end-to-end against a real Asterisk (opt-in via CALLORA_ASTERISK_TESTS=1). Proves the
/// CalloraVoipSdk client actually registers a SIP account, which is what makes an SdkVoiceChannel
/// report Health.Up and the runtime-capability mechanism grant communication.voice.
/// Start Asterisk first: docker run --network host ... (see ops/spikes/asterisk-b4deep3).
/// </summary>
[Trait("Category", "Asterisk")]
public sealed class AsteriskVoiceIntegrationTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("CALLORA_ASTERISK_TESTS") == "1";

    [SkippableFact]
    public async Task Client_RegistersSipAccount_AgainstAsterisk()
    {
        Skip.IfNot(Enabled, "Set CALLORA_ASTERISK_TESTS=1 with a running Asterisk to run this.");

        using var client = new VoipClient(new VoipConfiguration
        {
            DefaultTransport = SipTransport.Udp,
            PreferredAudioCodecs = ["PCMU"],
            BridgeAudioFormat = BridgeAudioFormat.Pcmu,
        });

        var account = new SipAccount
        {
            Username = "callora",
            Password = "callora",
            SipServer = "127.0.0.1",
            Port = 5060,
            Transport = CoreSipTransport.Udp,
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var result = await client.ConnectAsync(account, ConnectOptions.Default, cts.Token);

        Assert.True(result.IsSuccess, $"Registration failed: status={result.Status}, line state={result.FinalLineState}, error={result.Error?.Message}");
        Assert.NotNull(result.Line);
        Assert.Equal(LineState.Registered, result.Line!.State);
    }
}
