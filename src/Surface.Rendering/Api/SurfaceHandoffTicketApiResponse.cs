namespace Callora.Surface.Rendering.Api;

/// <summary>
/// A minted handoff ticket, expressed as the URL to send the visitor to
/// (ADR-017 §8.4). The secret appears only here and in that URL.
/// </summary>
/// <param name="RedeemUrl">Absolute URL on the target host that exchanges the ticket for a session.</param>
/// <param name="TargetSurfaceKey">Surface the ticket was minted for.</param>
/// <param name="ExpiresAtUtc">When the ticket stops being redeemable.</param>
public sealed record SurfaceHandoffTicketApiResponse(
    string RedeemUrl,
    string TargetSurfaceKey,
    DateTimeOffset ExpiresAtUtc);
