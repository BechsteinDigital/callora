namespace Callora.Core.Application.Surfaces;

/// <summary>
/// A redeemable handoff ticket as the application sees it (ADR-017 §8.4): the
/// identity it carries plus the exact place it may be redeemed.
/// </summary>
/// <param name="TicketId">Storage id of the ticket.</param>
/// <param name="TenantKey">Tenant both surfaces belong to.</param>
/// <param name="WorkspaceKey">Workspace both surfaces belong to.</param>
/// <param name="SourceSurfaceKey">Surface the ticket was issued from.</param>
/// <param name="TargetSurfaceKey">Surface the ticket may be redeemed at.</param>
/// <param name="TargetAudience">Host the ticket may be redeemed on.</param>
/// <param name="Subject">Issuer and subject of the carried identity.</param>
/// <param name="Identity">The carried identity, including its own expiry.</param>
/// <param name="IssuedAtUtc">When the ticket was minted.</param>
/// <param name="ExpiresAtUtc">When the ticket stops being redeemable.</param>
public sealed record SurfaceHandoffTicket(
    Guid TicketId,
    string TenantKey,
    string WorkspaceKey,
    string SourceSurfaceKey,
    string TargetSurfaceKey,
    string TargetAudience,
    SurfaceSubject Subject,
    SurfaceIdentity Identity,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc);
