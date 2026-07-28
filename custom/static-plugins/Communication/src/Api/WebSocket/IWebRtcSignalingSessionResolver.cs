namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// Resolves the authorizer-approved connection subject to the workspace context a WebRTC signalling
/// session runs in (its client, target channel, call identity). This is the WebRTC counterpart to the
/// media path's session store: the handler stays free of provisioning/registry knowledge, which is wired
/// in a later slice (S4). Returns <see langword="null"/> when the subject no longer resolves (the channel
/// was deprovisioned between accept and handling), so the handler closes cleanly.
/// </summary>
public interface IWebRtcSignalingSessionResolver
{
    /// <summary>
    /// Resolves the session for the given connection <paramref name="subject"/> (the value the route's
    /// authorizer flowed onto the accepted connection), or <see langword="null"/> if it no longer resolves.
    /// </summary>
    ValueTask<WebRtcSignalingSession?> ResolveAsync(string? subject, CancellationToken cancellationToken = default);
}
