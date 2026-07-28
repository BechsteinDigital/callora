namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// A short-lived, single-use credential that authorizes one browser-initiated WebRTC signalling
/// connection. Minted by <see cref="IWebRtcSessionMinter"/> and consumed atomically by the
/// signalling authorizer; once used — or past its TTL — the token cannot be redeemed again.
/// </summary>
/// <param name="ConnectToken">
/// The opaque, cryptographically-random token the browser supplies on the signalling WebSocket
/// route (<c>/ws/communication/webrtc/{connectToken}</c>).
/// </param>
/// <param name="ExpiresInSeconds">
/// Advisory lifetime of the token in seconds (mirrors the server-side TTL). The browser should
/// connect before this window closes; the server enforces the deadline independently.
/// </param>
public sealed record WebRtcSessionTicket(string ConnectToken, int ExpiresInSeconds);
