namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Well-known capability codes used in channel declarations and plugin
/// registry manifests (requiresCapabilities/capabilities).
/// </summary>
public static class CommunicationCapabilities
{
    /// <summary>
    /// The communication foundation is present: persistence, the operator/admin surface, GDPR
    /// purge and the WebSocket media surface. This is what the plugin honestly provides today. It
    /// does <b>not</b> imply that a call can be placed — capabilities express a hard technical
    /// dependency, so a consumer must not treat this as voice availability.
    /// </summary>
    public const string Foundation = "communication.foundation";

    /// <summary>
    /// Outbound and inbound voice calls. Declared in the manifest only once a working voice
    /// channel is actually registered (the SDK/RTP bridge, B4-deep) — until then the plugin
    /// advertises <see cref="Foundation"/> instead, so a dialer/AI-agent cannot activate against a
    /// capability that no runtime can satisfy.
    /// </summary>
    public const string Voice = "communication.voice";

    /// <summary>
    /// Multi-party or point-to-point video communication. Providers implement the media technology
    /// behind their own adapter; the Communication foundation remains SDK-neutral.
    /// </summary>
    public const string Video = "communication.video";

    /// <summary>
    /// Browser WebRTC connectivity supplied by a communication-channel adapter. This capability says
    /// nothing about the concrete WebRTC SDK used by that provider.
    /// </summary>
    public const string WebRtc = "communication.webrtc";

    /// <summary>
    /// A call can be put into a conference, so that somebody on a telephone takes part in a room of
    /// browsers.
    /// </summary>
    /// <remarks>
    /// Unlike the others this one is derived rather than declared, because no single channel can claim
    /// it: bridging needs telephony and a conference to be available at the same time, plus the
    /// attachment that joins them. A consumer requiring this one is saying it needs all three — which
    /// is exactly what <see cref="Foundation"/> does not tell it.
    /// </remarks>
    public const string ConferenceTelephony = "communication.conference.telephony";
}
