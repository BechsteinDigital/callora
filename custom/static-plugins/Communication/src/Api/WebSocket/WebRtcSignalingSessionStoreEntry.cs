namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// One stored WebRTC signalling session plus the state the <see cref="WebRtcSignalingSessionStore"/>
/// needs to enforce single-use and TTL. A class (not a struct/record) so <see cref="Consumed"/> can be
/// flipped in place via <see cref="System.Threading.Interlocked"/> without re-inserting the entry.
/// </summary>
internal sealed class WebRtcSignalingSessionStoreEntry(WebRtcSignalingSession session, DateTimeOffset createdAt)
{
    /// <summary>The session handed back to the resolver once the token is consumed.</summary>
    public readonly WebRtcSignalingSession Session = session;

    /// <summary>Mint timestamp, evaluated against the TTL at consume time.</summary>
    public readonly DateTimeOffset CreatedAt = createdAt;

    /// <summary>0 = unconsumed; 1 = consumed. Modified only via <see cref="System.Threading.Interlocked.Exchange(ref int, int)"/>.</summary>
    public int Consumed;
}
