namespace Callora.Core.Application.Security;

/// <summary>
/// Bounded protection against credential guessing (#104). Consecutive failures on
/// one account lock it for a fixed window; a success clears the counter. Deliberately
/// account-scoped and time-bounded: it slows guessing without letting an attacker
/// lock a known account out indefinitely. Per-IP throttling is the rate limiter's job.
/// </summary>
public static class BackendLockoutPolicy
{
    /// <summary>Consecutive failures that trigger a lockout.</summary>
    public const int MaxFailedAttempts = 10;

    /// <summary>How long the account stays locked once the threshold is reached.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
}
