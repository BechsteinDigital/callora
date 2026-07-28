namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Mints a short-lived, single-use connect-ticket for a browser-initiated WebRTC signalling
/// session in a workspace. The Communication foundation provides this primitive; consumers
/// (videoconference plugin, Admin-Shell softphone) mint through it without knowing the
/// signalling infrastructure behind it.
/// </summary>
/// <remarks>
/// Minting is a synchronous, in-memory operation — no I/O, no persistence, no awaitable.
/// The returned <see cref="WebRtcSessionTicket"/> carries an opaque connect-token and its
/// advisory TTL; the token is consumed atomically by the signalling authorizer and cannot
/// be redeemed a second time.
/// </remarks>
public interface IWebRtcSessionMinter
{
    /// <summary>
    /// Mints a connect-ticket for the workspace identified by <paramref name="workspaceKey"/>.
    /// </summary>
    /// <param name="workspaceKey">
    /// The workspace the session belongs to; determines which voice channel the resulting call
    /// is tracked on.
    /// </param>
    /// <param name="target">
    /// The remote participant the resulting inbound call is attributed to (shown in call history
    /// and forwarded to consumers).
    /// </param>
    /// <param name="callId">
    /// Optional stable call identity; when <see langword="null"/> or whitespace a random id is
    /// generated. Pass a value to correlate a signalling round-trip with an earlier booking.
    /// </param>
    /// <returns>A fresh, single-use <see cref="WebRtcSessionTicket"/>.</returns>
    WebRtcSessionTicket MintSession(string workspaceKey, CallTarget target, string? callId = null);
}
