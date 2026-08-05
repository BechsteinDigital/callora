namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// Hard resource bounds for the plugin's WebSocket surfaces (#108). A valid ticket
/// holder is still an untrusted peer: without these, one connection can grow host
/// memory without limit through fragmented messages, oversized audio payloads or a
/// producer that outruns the paced sender.
/// <para>
/// The values are deliberately generous against legitimate traffic and tiny against
/// abuse: a 20 ms µ-law frame is 160 bytes (~216 base64), and an SDP offer with
/// bundled ICE candidates stays far below 64 KiB.
/// </para>
/// </summary>
public static class CommunicationStreamLimits
{
    /// <summary>
    /// Largest media-protocol message accepted, across all fragments. Exceeding it
    /// aborts the connection rather than truncating — a peer sending more is either
    /// broken or hostile.
    /// </summary>
    public const int MaxMediaMessageBytes = 64 * 1024;

    /// <summary>Largest signalling message accepted, across all fragments.</summary>
    public const int MaxSignalingMessageBytes = 64 * 1024;

    /// <summary>
    /// Largest decoded audio frame accepted. Well above any 20–60 ms frame in the
    /// supported formats, and far below anything that could pressure memory.
    /// </summary>
    public const int MaxAudioFrameBytes = 8 * 1024;

    /// <summary>
    /// Total bytes the paced outbound buffer may hold. Bounds the queue by size as
    /// well as by frame count, so many small frames cannot bypass the count cap.
    /// </summary>
    public const int MaxPacedBufferBytes = 512 * 1024;

    /// <summary>
    /// How long a socket may stay silent before it is torn down. Frees sockets that a
    /// peer opened and abandoned, which would otherwise hold their buffers forever.
    /// </summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long a connect token stays redeemable after the session is minted.</summary>
    public static readonly TimeSpan ConnectTokenTimeToLive = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Sessions are removed this long after they closed or their token expired. Keeps
    /// spent tickets from accumulating and shrinks the window in which a leaked row is
    /// worth anything.
    /// </summary>
    public static readonly TimeSpan SessionRetention = TimeSpan.FromHours(24);
}
