namespace Callora.Plugin.Communication.Domain.Accounts;

/// <summary>
/// One origin's share of an account's lines.
/// </summary>
/// <remarks>
/// <para>The account's own limit is the ceiling; a quota only decides who reaches it first. The case
/// that matters is a dialer working a campaign against an agent trying to answer, because the dialer
/// never waits and the agent can only ever wait.</para>
/// <para><b>Shares may add up to more than the account has.</b> Dividing exactly would leave lines
/// idle whenever one origin is quiet, which is the opposite of what an operator splitting a trunk
/// wants.</para>
/// </remarks>
public sealed record CallQuota
{
    /// <summary>Creates one share.</summary>
    /// <param name="origin">
    /// What is claiming the line, as the calling plugin names it — <c>crm</c>, or a finer
    /// <c>dialer:campaign-x</c> when one consumer runs several things that should not exhaust each
    /// other. Matched ordinally against what the plugin passes, so it is trimmed but not case-folded.
    /// </param>
    /// <param name="maxConcurrentCalls">
    /// How many lines this origin may hold at once. At least one: zero lines is not a share but a ban,
    /// and an origin that should not call at all does not get a quota — it gets no code path.
    /// </param>
    public CallQuota(string origin, int maxConcurrentCalls)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentCalls, 1);

        Origin = origin.Trim();
        MaxConcurrentCalls = maxConcurrentCalls;
    }

    /// <summary>The origin this share belongs to.</summary>
    public string Origin { get; }

    /// <summary>Lines this origin may hold at once.</summary>
    public int MaxConcurrentCalls { get; }

    /// <summary>
    /// Validates a whole set of shares and hands back a stable list. Rejects the same origin twice:
    /// one of the two would silently win, and which one would depend on ordering.
    /// </summary>
    /// <param name="quotas">The configured shares, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">The same origin appears more than once.</exception>
    public static IReadOnlyList<CallQuota> Validate(IEnumerable<CallQuota>? quotas)
    {
        if (quotas is null)
        {
            return [];
        }

        var validated = new List<CallQuota>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var quota in quotas)
        {
            ArgumentNullException.ThrowIfNull(quota);

            if (!seen.Add(quota.Origin))
            {
                throw new ArgumentException(
                    $"Origin '{quota.Origin}' has more than one quota.", nameof(quotas));
            }

            validated.Add(quota);
        }

        return validated;
    }
}
