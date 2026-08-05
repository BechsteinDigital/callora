namespace Callora.Plugin.Communication.Application.Admin.WebRtc;

/// <summary>
/// Body of <c>POST webrtc/sessions</c>.
/// </summary>
/// <param name="Target">
/// Who the resulting inbound call is attributed to in history, for example the browser user's
/// handle. Defaults to the caller's workspace when omitted.
/// </param>
/// <param name="DisplayName">Optional human-readable participant name.</param>
/// <param name="CallId">
/// Optional stable call identity, to correlate the signalling round-trip with an earlier booking. A
/// random id is generated when omitted.
/// </param>
public sealed record MintWebRtcSessionApiRequest(string? Target, string? DisplayName, string? CallId);
