namespace Callora.Core.Domain.Security;

/// <summary>
/// One revoked session, identified by the JWT <c>jti</c> it was issued with (#105).
/// Rows survive a restart — otherwise a logged-out token would work again — and are
/// purged once <see cref="ExpiresAtUtc"/> has passed.
/// </summary>
public sealed class BackendRevokedSession
{
    /// <summary>The session's JWT identifier.</summary>
    public string TokenId { get; set; } = string.Empty;

    /// <summary>Owning account, for audit and bulk cleanup.</summary>
    public string? Subject { get; set; }

    /// <summary>When the underlying token expires; the row may be dropped afterwards.</summary>
    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>When the revocation was recorded.</summary>
    public DateTimeOffset RevokedAtUtc { get; set; }
}
