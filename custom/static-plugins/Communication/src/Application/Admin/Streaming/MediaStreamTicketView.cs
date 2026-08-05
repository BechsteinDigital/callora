using Callora.Plugin.Communication.Application.Streaming;

namespace Callora.Plugin.Communication.Application.Admin.Streaming;

/// <summary>
/// API shape of a minted media ticket: the token plus the socket path to redeem it on, so a
/// consumer needs no knowledge of the host's WebSocket layout.
/// </summary>
/// <param name="SessionId">Identity of the minted session; the correlation handle for logs and audits.</param>
/// <param name="CallId">The call this stream attaches to.</param>
/// <param name="ConnectToken">Single-use connect token. Consumed by the first connect.</param>
/// <param name="ConnectPath">Host-relative WebSocket path carrying the token.</param>
/// <param name="Direction">Audio flow the socket may carry, relative to the consumer.</param>
/// <param name="ExpiresInSeconds">Seconds the token stays redeemable.</param>
/// <param name="Encoding">Media type of the audio frames.</param>
/// <param name="SampleRateHz">Sample rate of the audio frames.</param>
public sealed record MediaStreamTicketView(
    string SessionId,
    string CallId,
    string ConnectToken,
    string ConnectPath,
    string Direction,
    int ExpiresInSeconds,
    string Encoding,
    int SampleRateHz)
{
    /// <summary>
    /// Host WebSocket prefix the media route lives under. Kept next to the view because it is part
    /// of the contract the response promises, not an implementation detail of the socket layer.
    /// </summary>
    public const string ConnectPathPrefix = "/ws/communication/media/";

    /// <summary>Projects a minted ticket into the API shape.</summary>
    public static MediaStreamTicketView From(MediaStreamTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        return new MediaStreamTicketView(
            ticket.SessionId,
            ticket.CallId,
            ticket.ConnectToken,
            ConnectPathPrefix + ticket.ConnectToken,
            ticket.Direction.ToString().ToLowerInvariant(),
            ticket.ExpiresInSeconds,
            "audio/x-mulaw",
            8000);
    }
}
