using Callora.Core.Application.Security;
using Callora.Core.Extensibility;
using System.Security.Claims;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Enforces session revocation on the request path (#105).
/// <para>
/// A token this host issued carries the account's security stamp
/// (<see cref="BackendClaimTypes.SecurityStamp"/>). The stamp is compared against the
/// stored one, so a password change, deactivation, deletion or RBAC change invalidates
/// every outstanding session immediately. The JWT <c>jti</c> is checked against the
/// revocation store, so logout kills exactly one session.
/// </para>
/// <para>
/// Tokens without a stamp claim are foreign credentials (external OIDC, named
/// integrations) and are left to their own issuer — this validator governs only the
/// sessions this host minted.
/// </para>
/// <para>
/// Bounded hot path: account state comes from <see cref="BackendSessionStateCache"/>,
/// so revocation costs at most one lookup per account per cache window and takes
/// effect within that window at the latest.
/// </para>
/// </summary>
[CalloraInternal("Session revocation enforcement — not a plugin contract (REV2 §7.2)")]
public sealed class BackendSessionValidator(
    IBackendUserStore userStore,
    IBackendSessionRevocationStore revocationStore,
    BackendSessionStateCache stateCache) : IBackendSessionValidator
{
    public async Task<string?> ValidateAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var sessionStamp = principal.FindFirst(BackendClaimTypes.SecurityStamp)?.Value;
        if (string.IsNullOrWhiteSpace(sessionStamp))
        {
            // Not a session this host issued — nothing to revoke here.
            return null;
        }

        var tokenId = principal.FindFirst(BackendClaimTypes.TokenId)?.Value;
        if (!string.IsNullOrWhiteSpace(tokenId) &&
            await revocationStore.IsRevokedAsync(tokenId, cancellationToken).ConfigureAwait(false))
        {
            return "The session was revoked.";
        }

        var subject = principal.FindFirst("sub")?.Value
                      ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            return "The session carries a security stamp but no subject.";
        }

        var state = await GetAccountStateAsync(subject, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            return "The account no longer exists.";
        }

        if (state.IsDisabled)
        {
            return "The account is disabled.";
        }

        return BackendSecurityStamp.Matches(state.SecurityStamp, sessionStamp)
            ? null
            : "The session was invalidated by a credential or authorization change.";
    }

    private async Task<BackendSessionAccountState?> GetAccountStateAsync(
        string subject,
        CancellationToken cancellationToken)
    {
        if (stateCache.TryGet(subject, out var cached))
        {
            return cached;
        }

        var user = await userStore.GetByExternalIdAsync(subject, cancellationToken).ConfigureAwait(false);
        var state = user is null ? null : new BackendSessionAccountState(user.SecurityStamp, user.IsDisabled);
        stateCache.Set(subject, state);
        return state;
    }
}
