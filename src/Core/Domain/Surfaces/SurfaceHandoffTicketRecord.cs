namespace Callora.Core.Domain.Surfaces;

/// <summary>
/// A one-time ticket that moves an established identity to a surface on another host
/// (ADR-017 §8.4). Surfaces can live on different hosts, so a cookie alone cannot
/// carry the visitor across; a long-lived bearer token circulating between all
/// surface hosts is the thing this exists to avoid.
/// <para>
/// The row stores only the hash of the secret. A leaked database therefore yields no
/// redeemable ticket, and the short lifetime plus single use bound the damage even
/// if the secret itself is intercepted.
/// </para>
/// </summary>
public sealed class SurfaceHandoffTicketRecord
{
    public Guid Id { get; set; }

    /// <summary>SHA-256 of the ticket secret, hex encoded. The secret itself is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public string TenantKey { get; set; } = string.Empty;

    public string WorkspaceKey { get; set; } = string.Empty;

    /// <summary>Surface the ticket was issued from, kept for audit.</summary>
    public string SourceSurfaceKey { get; set; } = string.Empty;

    /// <summary>Surface the ticket may be redeemed at.</summary>
    public string TargetSurfaceKey { get; set; } = string.Empty;

    /// <summary>Host the ticket may be redeemed on. Checked on redemption.</summary>
    public string TargetAudience { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string SubjectId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ClaimsJson { get; set; } = "{}";

    public string AuthenticationMethod { get; set; } = string.Empty;

    public DateTimeOffset AuthenticatedAtUtc { get; set; }

    /// <summary>When the carried identity expires; the redeemed session inherits it.</summary>
    public DateTimeOffset IdentityExpiresAtUtc { get; set; }

    public DateTimeOffset IssuedAtUtc { get; set; }

    /// <summary>When the ticket stops being redeemable. Deliberately short.</summary>
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
