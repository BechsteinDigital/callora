using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Accounts;
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
/// B4-deep-3 audio round-trip against a real Asterisk (opt-in via CALLORA_ASTERISK_TESTS=1): places an
/// outbound call from the real SDK-backed <see cref="SdkVoiceChannel"/> to the echo extension (600),
/// opens the foundation audio bridge (<see cref="ICallAudioStream"/>), streams a distinctive G.711 µ-law
/// pattern, and asserts the same bytes come back — proving the RTP↔media bridge (B4-deep-1) actually
/// round-trips real media end-to-end, not just the SIP registration. Start Asterisk with the spike
/// config first (see ops/spikes/asterisk-b4deep3, dialplan extension 600 = Answer/Echo/Hangup).
/// </summary>
[Trait("Category", "Asterisk")]
public sealed class AsteriskAudioEchoIntegrationTests
{
    private const string PluginId = "communication";
    private const int UlawFrameBytes = 160; // G.711 µ-law, 8 kHz, 20 ms.

    private static bool Enabled => Environment.GetEnvironmentVariable("CALLORA_ASTERISK_TESTS") == "1";

    [SkippableFact]
    public async Task OutboundCall_ToEchoExtension_RoundTripsAudio()
    {
        Skip.IfNot(Enabled, "Set CALLORA_ASTERISK_TESTS=1 with a running Asterisk to run this.");

        using var client = new VoipClient(new VoipConfiguration
        {
            DefaultTransport = SdkSipTransport.Udp,
            PreferredAudioCodecs = ["PCMU"],
            BridgeAudioFormat = BridgeAudioFormat.Pcmu,
        });

        var connector = new SdkVoiceChannelConnector(
            new SdkSipAccountFactory(new FakePluginDataProtector(("pw-ref", "callora")), PluginId),
            new VoipClientVoiceRuntime(client),
            PluginId,
            NullLogger<SdkVoiceChannelConnector>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var channel = await connector.ConnectAsync(NewAccount(), cts.Token);
        Assert.NotNull(channel);
        try
        {
            var call = (IVoipCall)await channel!.PlaceCallAsync(new CallTarget("sip:600@127.0.0.1"), cts.Token);
            try
            {
                await WaitForStateAsync(call, CallState.Connected, cts.Token);

                var received = new List<byte>();
                var gate = new object();
                await using var audio = await call.OpenAudioAsync(cts.Token);
                audio.FrameReceived += (_, e) =>
                {
                    var copy = e.Frame.ToArray(); // frame memory is only valid during the callback
                    lock (gate)
                    {
                        received.AddRange(copy);
                    }
                };

                // A deterministic, non-silence pattern so the echo is unmistakable in the inbound stream.
                var payload = new byte[UlawFrameBytes];
                for (var i = 0; i < payload.Length; i++)
                {
                    payload[i] = (byte)((i * 31 + 7) & 0xFF);
                }

                // Stream ~2 s of the pattern at the 20 ms frame cadence (Task.Delay is fine for a test;
                // production consumers pace off a monotonic clock per the ICallAudioStream contract).
                for (var n = 0; n < 100 && !cts.IsCancellationRequested; n++)
                {
                    await audio.SendAsync(payload, cts.Token);
                    await Task.Delay(20, cts.Token);
                }

                var found = await WaitUntilAsync(
                    () => { lock (gate) { return ContainsSubsequence(received, payload); } },
                    TimeSpan.FromSeconds(5),
                    cts.Token);

                Assert.True(found, "The µ-law pattern sent to the echo extension did not round-trip back.");
            }
            finally
            {
                await call.HangupAsync(CancellationToken.None);
            }
        }
        finally
        {
            (channel as IDisposable)?.Dispose();
        }
    }

    private static SipAccount NewAccount()
    {
        var auth = new DigestAuthentication("callora", authId: null, passwordSecretRef: "pw-ref");
        var connection = new SipConnection(
            "127.0.0.1", 5060, SipTransport.Udp, SipAccountMode.Register, auth, registrationExpirySeconds: 600);
        return new SipAccount("acc-asterisk", "ws-asterisk", "Asterisk Line", connection, maxConcurrentCalls: 1, enabled: true);
    }

    // Resolves when the call reaches <paramref name="target"/>; throws if it terminates first or times out.
    private static async Task WaitForStateAsync(ICall call, CallState target, CancellationToken ct)
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(object? sender, CallStateChangedEventArgs e)
        {
            if (e.CurrentState == target || e.CurrentState == CallState.Terminated)
            {
                reached.TrySetResult();
            }
        }

        call.StateChanged += Handler;
        try
        {
            if (call.State != target) // guard the race: it may have transitioned before we subscribed
            {
                using var reg = ct.Register(() => reached.TrySetCanceled(ct));
                await reached.Task.ConfigureAwait(false);
            }

            if (call.State != target)
            {
                throw new InvalidOperationException($"Call reached {call.State} before {target}.");
            }
        }
        finally
        {
            call.StateChanged -= Handler;
        }
    }

    // Polls <paramref name="predicate"/> until it holds or the budget elapses; no ambient clock needed.
    private static async Task<bool> WaitUntilAsync(Func<bool> predicate, TimeSpan budget, CancellationToken ct)
    {
        var attempts = (int)(budget.TotalMilliseconds / 50);
        for (var i = 0; i < attempts; i++)
        {
            if (predicate())
            {
                return true;
            }

            await Task.Delay(50, ct).ConfigureAwait(false);
        }

        return predicate();
    }

    private static bool ContainsSubsequence(List<byte> haystack, byte[] needle)
    {
        if (haystack.Count < needle.Length)
        {
            return false;
        }

        for (var start = 0; start <= haystack.Count - needle.Length; start++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[start + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }
}
