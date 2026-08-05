namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// API shape of a minted call-event ticket: the token plus the socket path to redeem it on.
/// </summary>
/// <param name="ConnectToken">Single-use ticket. Consumed by the first connect.</param>
/// <param name="ConnectPath">Host-relative WebSocket path carrying the ticket.</param>
/// <param name="ExpiresInSeconds">Seconds the ticket stays redeemable.</param>
public sealed record CallEventStreamTicketView(string ConnectToken, string ConnectPath, int ExpiresInSeconds)
{
    /// <summary>Host WebSocket prefix the call-event route lives under.</summary>
    public const string ConnectPathPrefix = "/ws/communication/calls/";

    /// <summary>Builds the view for a freshly minted ticket.</summary>
    public static CallEventStreamTicketView For(string connectToken, TimeSpan timeToLive) =>
        new(connectToken, ConnectPathPrefix + connectToken, (int)timeToLive.TotalSeconds);
}
