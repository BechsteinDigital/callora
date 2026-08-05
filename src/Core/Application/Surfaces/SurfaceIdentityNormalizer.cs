using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Turns a provider's identity <em>candidate</em> into a caller the host is willing
/// to carry (ADR-017 §4). Everything a provider returns passes through here: issuer
/// namespace, subject shape, timestamps, lifetime clamp and claim bounds. A provider
/// that stays inside the bounds is never second-guessed; one that exceeds them is
/// rejected rather than silently truncated, because a quietly shortened identity is
/// harder to diagnose than a refused one.
/// </summary>
internal sealed class SurfaceIdentityNormalizer
{
    private readonly SurfaceIdentityOptions _options;
    private readonly SurfaceIdentityClaimNormalizer _claimNormalizer;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates the normaliser.
    /// </summary>
    /// <param name="options">Host bounds on identity shape and lifetime.</param>
    /// <param name="claimNormalizer">Validator for the candidate's claim bag.</param>
    /// <param name="timeProvider">Clock used for expiry and skew checks.</param>
    public SurfaceIdentityNormalizer(
        SurfaceIdentityOptions options,
        SurfaceIdentityClaimNormalizer claimNormalizer,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(claimNormalizer);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _options = options;
        _claimNormalizer = claimNormalizer;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Validates a candidate and produces an authenticated caller.
    /// </summary>
    /// <param name="candidate">What the provider returned.</param>
    /// <param name="allowReservedIssuer">
    /// Only the host's own identity source may issue under <c>callora.</c>; a plugin
    /// provider is refused there so it cannot impersonate the platform.
    /// </param>
    public SurfaceIdentityNormalization Normalize(
        HostSurfaceIdentityResult candidate,
        bool allowReservedIssuer = false)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!candidate.IsIdentified)
        {
            return SurfaceIdentityNormalization.NotIdentified;
        }

        var subject = NormalizeSubject(candidate, allowReservedIssuer, out var subjectRejection);
        if (subject is null)
        {
            return subjectRejection!;
        }

        if (!SurfaceIdentityTokenSyntax.IsToken(candidate.AuthenticationMethod, _options.MaxIdentifierLength))
        {
            return SurfaceIdentityNormalization.Reject(
                SurfaceIdentityRejectionReason.InvalidAuthenticationMethod,
                "Authentication method is missing or malformed.");
        }

        var window = NormalizeWindow(candidate, out var windowRejection);
        if (window is null)
        {
            return windowRejection!;
        }

        var claims = _claimNormalizer.Normalize(candidate.Claims);
        if (claims.Claims is null)
        {
            return SurfaceIdentityNormalization.Reject(claims.Reason, claims.Detail);
        }

        var displayName = NormalizeDisplayName(candidate.DisplayName, subject.SubjectId);
        var identity = new SurfaceIdentity(
            displayName,
            claims.Claims,
            candidate.AuthenticationMethod!,
            window.AuthenticatedAtUtc,
            window.ExpiresAtUtc);

        return SurfaceIdentityNormalization.Accept(new AuthenticatedSurfaceCaller(subject, identity));
    }

    private SurfaceSubject? NormalizeSubject(
        HostSurfaceIdentityResult candidate,
        bool allowReservedIssuer,
        out SurfaceIdentityNormalization? rejection)
    {
        var issuer = candidate.Issuer?.Trim();
        if (!allowReservedIssuer && SurfaceIdentityIssuers.IsReserved(issuer))
        {
            rejection = SurfaceIdentityNormalization.Reject(
                SurfaceIdentityRejectionReason.ReservedIssuer,
                $"Issuer '{issuer}' uses the reserved host namespace.");
            return null;
        }

        if (!SurfaceIdentityTokenSyntax.IsToken(issuer, _options.MaxIdentifierLength))
        {
            rejection = SurfaceIdentityNormalization.Reject(
                SurfaceIdentityRejectionReason.InvalidIssuer,
                "Issuer is missing or malformed.");
            return null;
        }

        var subjectId = candidate.SubjectId?.Trim();
        if (string.IsNullOrEmpty(subjectId) ||
            !SurfaceIdentityTokenSyntax.IsPrintable(subjectId, _options.MaxIdentifierLength))
        {
            rejection = SurfaceIdentityNormalization.Reject(
                SurfaceIdentityRejectionReason.InvalidSubject,
                "Subject id is missing or malformed.");
            return null;
        }

        rejection = null;
        return new SurfaceSubject(issuer!, subjectId);
    }

    private SurfaceIdentityWindow? NormalizeWindow(
        HostSurfaceIdentityResult candidate,
        out SurfaceIdentityNormalization? rejection)
    {
        if (candidate.AuthenticatedAtUtc is not { } authenticatedAt ||
            candidate.ExpiresAtUtc is not { } expiresAt)
        {
            rejection = SurfaceIdentityNormalization.Reject(
                SurfaceIdentityRejectionReason.InvalidTimestamps,
                "Authentication or expiry timestamp is missing.");
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        if (authenticatedAt > now + _options.ClockSkew)
        {
            rejection = SurfaceIdentityNormalization.Reject(
                SurfaceIdentityRejectionReason.InvalidTimestamps,
                "Authentication timestamp lies in the future.");
            return null;
        }

        if (expiresAt <= now)
        {
            rejection = SurfaceIdentityNormalization.Reject(
                SurfaceIdentityRejectionReason.Expired,
                "Identity was already expired when it arrived.");
            return null;
        }

        // The provider proposes an expiry, the host caps it. Without the cap a
        // provider could mint a session that outlives any policy the operator set.
        var ceiling = now + _options.MaxIdentityLifetime;
        rejection = null;
        return new SurfaceIdentityWindow(
            authenticatedAt > now ? now : authenticatedAt,
            expiresAt > ceiling ? ceiling : expiresAt);
    }

    private string NormalizeDisplayName(string? displayName, string subjectId)
    {
        var trimmed = displayName?.Trim();
        return SurfaceIdentityTokenSyntax.IsPrintable(trimmed, _options.MaxIdentifierLength) &&
               !string.IsNullOrEmpty(trimmed)
            ? trimmed
            : subjectId;
    }
}
