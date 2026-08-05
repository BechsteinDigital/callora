using Callora.Plugin.Communication.Domain.Streaming;

namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// The result of minting a media stream: the one-time connect token plus everything the consumer
/// needs to open and interpret the socket. The token exists only here and in the response that
/// carries it — the session row keeps a hash (#108).
/// </summary>
public sealed class MediaStreamTicket
{
    /// <summary>Creates a ticket for a freshly minted session.</summary>
    public MediaStreamTicket(
        string sessionId,
        string callId,
        string connectToken,
        MediaStreamDirection direction,
        int expiresInSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectToken);

        SessionId = sessionId;
        CallId = callId;
        ConnectToken = connectToken;
        Direction = direction;
        ExpiresInSeconds = expiresInSeconds;
    }

    /// <summary>Identity of the minted session; safe to log and to correlate on.</summary>
    public string SessionId { get; }

    /// <summary>The call this stream will attach to.</summary>
    public string CallId { get; }

    /// <summary>
    /// Single-use connect token. Redeemed by opening
    /// <c>/ws/communication/media/{connectToken}</c>; consumed by the first connect.
    /// </summary>
    public string ConnectToken { get; }

    /// <summary>Audio flow the socket is allowed to carry, relative to the consumer.</summary>
    public MediaStreamDirection Direction { get; }

    /// <summary>Seconds the token stays redeemable.</summary>
    public int ExpiresInSeconds { get; }

    /// <summary>
    /// Deliberately omits the token, so an audit line or a log statement that interpolates the
    /// ticket cannot leak a live credential (#114).
    /// </summary>
    public override string ToString() =>
        $"MediaStreamTicket {{ SessionId = {SessionId}, CallId = {CallId}, Direction = {Direction}, ExpiresInSeconds = {ExpiresInSeconds} }}";
}
