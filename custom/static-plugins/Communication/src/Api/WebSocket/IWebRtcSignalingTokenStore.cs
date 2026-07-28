namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// Validates (and atomically consumes) a WebRTC signalling connect-token, the WebRTC counterpart to the
/// media path's session store. A single token authorizes exactly one connect: on success it returns the
/// resolved connection subject (the workspace/channel principal the handler resolves its session from);
/// an unknown, expired or already-used token returns <see langword="null"/> so the authorizer denies
/// fail-closed. The concrete store (minting and persistence) is wired in a later slice (S4).
/// </summary>
public interface IWebRtcSignalingTokenStore
{
    /// <summary>
    /// Consumes <paramref name="connectToken"/> if it is valid at <paramref name="now"/>, returning the
    /// resolved connection subject, or <see langword="null"/> if it is unknown, expired or already used.
    /// </summary>
    ValueTask<string?> TryConsumeAsync(
        string connectToken,
        DateTimeOffset now,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default);
}
