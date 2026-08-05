namespace Callora.Core.Application.Security;

/// <summary>
/// Records individually revoked sessions (#105). Logout revokes exactly the session
/// that was used, leaving the account's other sessions alive; bulk revocation of an
/// account runs through the security stamp instead.
/// <para>
/// Entries are bounded by the token lifetime: an entry may be dropped once the token
/// it names has expired, because an expired token is rejected by signature validation
/// anyway.
/// </para>
/// </summary>
public interface IBackendSessionRevocationStore
{
    /// <summary>Marks <paramref name="tokenId"/> revoked until it expires. Idempotent.</summary>
    Task RevokeAsync(string tokenId, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Whether the session was revoked and its token has not expired yet.</summary>
    Task<bool> IsRevokedAsync(string tokenId, CancellationToken cancellationToken = default);

    /// <summary>Drops entries whose tokens have expired. Safe to call repeatedly.</summary>
    Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default);
}
