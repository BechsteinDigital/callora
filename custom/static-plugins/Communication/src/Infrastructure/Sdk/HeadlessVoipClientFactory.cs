using CalloraVoipSdk;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Builds the server-side CalloraVoipSdk client the plugin registers voice accounts through when a
/// deployment enables voice. The configuration is the one validated end-to-end in B4-deep-3: UDP
/// signalling and G.711 µ-law media (<c>PCMU</c>), with the default silence audio device — media flows
/// through each call's media tap (the RTP↔bridge), not a hardware device, so a headless server needs
/// no audio hardware. The caller owns the returned client and must dispose it.
/// </summary>
public static class HeadlessVoipClientFactory
{
    /// <summary>Creates a new, unconnected voice client. Registration happens per account afterwards.</summary>
    public static IVoipClient Create() => new VoipClient(new VoipConfiguration
    {
        DefaultTransport = SipTransport.Udp,
        PreferredAudioCodecs = ["PCMU"],
        BridgeAudioFormat = BridgeAudioFormat.Pcmu,
    });
}
