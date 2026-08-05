using Callora.Plugin.Communication.Application.WebRtc;

namespace Callora.Plugin.Communication.Application.Admin.WebRtc;

/// <summary>
/// API shape of a minted WebRTC session: the signalling ticket plus the ICE configuration for the
/// browser's <c>RTCPeerConnection</c>.
/// </summary>
/// <param name="ConnectToken">Single-use signalling token.</param>
/// <param name="ConnectPath">Host-relative signalling WebSocket path carrying the token.</param>
/// <param name="ExpiresInSeconds">Seconds the token stays redeemable.</param>
/// <param name="IceServers">
/// STUN/TURN servers with per-session credentials where the deployment configured a shared secret.
/// </param>
/// <param name="IceCredentialExpiresInSeconds">
/// Lifetime of the TURN credentials above. Null when no server issues short-lived credentials, which
/// is the honest answer for a deployment relying on static ones.
/// </param>
public sealed record WebRtcSessionView(
    string ConnectToken,
    string ConnectPath,
    int ExpiresInSeconds,
    IReadOnlyList<IceServerView> IceServers,
    int? IceCredentialExpiresInSeconds)
{
    /// <summary>Host WebSocket prefix the signalling route lives under.</summary>
    public const string ConnectPathPrefix = "/ws/communication/webrtc/";
}
