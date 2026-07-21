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
}
