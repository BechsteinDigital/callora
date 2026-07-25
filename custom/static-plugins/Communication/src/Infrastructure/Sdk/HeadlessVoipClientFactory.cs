using CalloraVoipSdk;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Builds the server-side CalloraVoipSdk client the plugin registers voice accounts through when a
/// deployment enables voice. The signalling transport and media-security posture come from
/// <see cref="VoiceClientOptions"/> (the <c>Communication:Voice</c> config); codec and bridge format
/// stay G.711 µ-law (<c>PCMU</c>) because the media bridge (<c>SdkCallAudioStream</c>) is µ-law only.
/// The default silence audio device means a headless server needs no audio hardware — media flows
/// through each call's media tap (the RTP↔bridge). The caller owns the returned client and disposes it.
/// </summary>
internal static class HeadlessVoipClientFactory
{
    /// <summary>Creates a new, unconnected voice client from the given options.</summary>
    public static IVoipClient Create(VoiceClientOptions options) => new VoipClient(BuildConfiguration(options));

    /// <summary>Maps the deployment options onto the SDK configuration (codec/bridge fixed to PCMU).</summary>
    public static VoipConfiguration BuildConfiguration(VoiceClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new VoipConfiguration
        {
            DefaultTransport = options.Transport,
            PreferredAudioCodecs = ["PCMU"],
            BridgeAudioFormat = BridgeAudioFormat.Pcmu,
            SrtpPolicy = options.SrtpPolicy,
            OfferDtlsSrtp = options.OfferDtlsSrtp,
            RequireSecureSignalingForSdes = options.RequireSecureSignalingForSdes,
            InboundMediaTimeout = options.InboundMediaTimeout,
        };
    }
}
