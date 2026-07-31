using CalloraVoipSdk.WebRtc;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Builds the server-side CalloraVoipSdk WebRTC client the plugin creates peer connections through when a
/// deployment enables browser voice. The counterpart to <see cref="HeadlessVoipClientFactory"/> for the
/// SIP facade: it maps the deployment <see cref="WebRtcClientOptions"/> (the plugin-scoped <c>WebRtc</c>
/// config) directly onto the SDK's immutable <see cref="WebRtcConfiguration"/> and constructs a
/// <see cref="WebRtcClient"/> — the WebRTC analogue of <c>new VoipClient(config)</c>, so no host DI
/// container is needed. The caller owns the returned client and disposes it asynchronously.
/// </summary>
/// <remarks>
/// WebRTC is Opus / transport-only: the SIP media bridge (µ-law <c>SdkCallAudioStream</c>) does NOT apply
/// here, so there is no PCMU pinning as on the SIP path. v1 is audio-focused; the video option is carried
/// through to the SDK but not wired to any media path (a future conferencing consumer will use it).
/// </remarks>
internal static class HeadlessWebRtcClientFactory
{
    /// <summary>Creates a new, unconnected WebRTC client from the given options.</summary>
    public static IWebRtcClient Create(WebRtcClientOptions options, ILoggerFactory? loggerFactory = null) =>
        new WebRtcClient(BuildConfiguration(options, loggerFactory));

    /// <summary>
    /// Maps the deployment options directly onto the SDK <see cref="WebRtcConfiguration"/> — the ICE
    /// servers, audio/video codecs, video flag and local endpoint modelled by
    /// <see cref="WebRtcClientOptions"/>. The remaining configuration fields (simulcast and DTLS identity)
    /// keep their SDK defaults.
    /// Kept a pure, testable function so the option-to-configuration mapping is verifiable without
    /// constructing a real client. The <paramref name="loggerFactory"/> is passed through when set so a
    /// later wiring slice can supply the host's factory for peer diagnostics.
    /// </summary>
    public static WebRtcConfiguration BuildConfiguration(WebRtcClientOptions options, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new WebRtcConfiguration
        {
            IceServers = options.IceServers,
            AudioCodecs = options.AudioCodecs,
            VideoCodecs = options.VideoCodecs,
            EnableVideo = options.EnableVideo,
            LocalEndPoint = options.LocalEndPoint,
            LoggerFactory = loggerFactory,
        };
    }
}
