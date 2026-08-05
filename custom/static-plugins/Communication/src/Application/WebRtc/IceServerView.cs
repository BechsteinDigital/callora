namespace Callora.Plugin.Communication.Application.WebRtc;

/// <summary>
/// One ICE server in the shape <c>RTCPeerConnection</c> expects, so a browser can pass the response
/// straight into <c>new RTCPeerConnection({ iceServers })</c>.
/// </summary>
/// <param name="Urls">ICE URLs of this server.</param>
/// <param name="Username">TURN username, absent for STUN.</param>
/// <param name="Credential">TURN credential, absent for STUN.</param>
public sealed record IceServerView(IReadOnlyList<string> Urls, string? Username, string? Credential);
