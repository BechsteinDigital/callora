namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Host-side bounds on what a surface identity provider may return, and how long the
/// host trusts it (ADR-017 §4, §8.1). These are guard rails, not policy: a provider
/// that stays inside them is never second-guessed, one that exceeds them is rejected
/// rather than silently truncated.
/// </summary>
public sealed class SurfaceIdentityOptions
{
    /// <summary>Configuration section binding these options.</summary>
    public const string SectionName = "Callora:SurfaceIdentity";

    /// <summary>
    /// Hard deadline for one provider call. A slow provider delays every render of
    /// its surface, so the wait is bounded and a timeout is a provider failure.
    /// </summary>
    public TimeSpan ProviderTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Upper bound on an authenticated session's lifetime. The effective expiry is
    /// <c>min(provider expiry, now + this)</c> — a provider cannot mint a long-lived
    /// session by claiming a distant expiry.
    /// </summary>
    public TimeSpan MaxIdentityLifetime { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// Lifetime of a guest context. Long by design: it carries no authority and its
    /// whole purpose is surviving between visits.
    /// </summary>
    public TimeSpan GuestContextLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Tolerance for clocks that disagree, applied to <c>AuthenticatedAt</c> so a
    /// marginally fast provider clock is not treated as a forged future timestamp.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Maximum number of distinct claim keys.</summary>
    public int MaxClaimCount { get; set; } = 64;

    /// <summary>Maximum number of values under one claim key.</summary>
    public int MaxClaimValuesPerKey { get; set; } = 16;

    /// <summary>Maximum length of a claim key.</summary>
    public int MaxClaimKeyLength { get; set; } = 128;

    /// <summary>Maximum length of a single claim value.</summary>
    public int MaxClaimValueLength { get; set; } = 1024;

    /// <summary>
    /// Total budget across all claim keys and values. Bounds what travels into the
    /// render context, the session record and every downstream request.
    /// </summary>
    public int MaxClaimTotalLength { get; set; } = 8 * 1024;

    /// <summary>Maximum length of issuer, subject id, display name and authentication method.</summary>
    public int MaxIdentifierLength { get; set; } = 200;
}
