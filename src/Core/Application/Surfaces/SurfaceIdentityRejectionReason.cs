namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Why the host refused a provider's identity candidate. Kept specific so an
/// operator can tell a misconfigured provider from a hostile one, and never
/// surfaced to the visitor (ADR-017 §4).
/// </summary>
internal enum SurfaceIdentityRejectionReason
{
    /// <summary>The candidate was accepted.</summary>
    None = 0,

    /// <summary>The provider reported no identity; not a rejection in itself.</summary>
    NotIdentified,

    /// <summary>Issuer missing, too long, or containing characters outside the allowed set.</summary>
    InvalidIssuer,

    /// <summary>A plugin provider tried to issue under the host's <c>callora.</c> namespace.</summary>
    ReservedIssuer,

    /// <summary>Subject id missing, too long, or containing control characters.</summary>
    InvalidSubject,

    /// <summary>Authentication method missing or malformed.</summary>
    InvalidAuthenticationMethod,

    /// <summary>A timestamp was missing or the authentication time lies in the future.</summary>
    InvalidTimestamps,

    /// <summary>The candidate was already expired when it arrived.</summary>
    Expired,

    /// <summary>More claim keys than the configured maximum.</summary>
    TooManyClaims,

    /// <summary>A claim key was empty, too long, or not namespaced.</summary>
    InvalidClaimKey,

    /// <summary>A claim key used the host's reserved <c>callora.</c> namespace.</summary>
    ReservedClaimKey,

    /// <summary>More values under one claim key than the configured maximum.</summary>
    TooManyClaimValues,

    /// <summary>A claim value was too long or contained control characters.</summary>
    InvalidClaimValue,

    /// <summary>The claims exceeded the total size budget.</summary>
    ClaimBudgetExceeded,
}
