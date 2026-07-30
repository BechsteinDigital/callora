namespace Callora.Plugin.Communication.Application.RealtimeMedia;

/// <summary>
/// A neutral STUN/TURN helper server for ICE gathering — the port's own value type, so
/// <see cref="MediaPeerOptions"/> carries no SDK configuration type. An adapter maps this onto its SDK's
/// ICE-server model.
/// </summary>
/// <param name="Host">The server host name or address.</param>
/// <param name="Port">The server port, or <see langword="null"/> for the scheme default.</param>
/// <param name="Kind">The server kind (<c>"stun"</c> or <c>"turn"</c>).</param>
/// <param name="Transport">The transport (<c>"udp"</c>, <c>"tcp"</c> or <c>"tls"</c>).</param>
/// <param name="Username">Optional TURN username.</param>
/// <param name="Password">Optional TURN credential.</param>
internal sealed record MediaIceServer(
    string Host,
    int? Port = null,
    string Kind = "stun",
    string Transport = "udp",
    string? Username = null,
    string? Password = null);
