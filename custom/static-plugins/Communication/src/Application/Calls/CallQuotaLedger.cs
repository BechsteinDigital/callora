using System.Collections.Concurrent;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Divides a trunk's lines between the things that use it, so one of them working through a queue
/// cannot take the lines another needs.
/// </summary>
/// <remarks>
/// <para>The account's own limit already stops everyone at the ceiling. This decides who reaches it
/// first — the case that matters is a dialer working a campaign against an agent trying to answer,
/// because the dialer never waits and the agent can only ever wait.</para>
/// <para><b>Quotas may add up to more than the trunk has.</b> Dividing exactly would leave lines idle
/// whenever one origin is quiet, which is the opposite of what an operator splitting them wants. The
/// account limit stays the real ceiling; a quota only says how much of it one origin may claim.</para>
/// <para><b>An origin without a quota is unlimited.</b> Splitting a trunk is deliberate, and an
/// operator who configured nothing wanted no split — not a silent limit of zero.</para>
/// </remarks>
public sealed class CallQuotaLedger : ICallQuotaRegistry
{
    private readonly ConcurrentDictionary<CallQuotaKey, int> _limits = new();
    private readonly ConcurrentDictionary<CallQuotaKey, int> _inUse = new();

    /// <inheritdoc />
    /// <remarks>
    /// Takes effect for the next reservation: an operator raising a quota expects it to apply now, not
    /// once the calls running under the old one have ended.
    /// </remarks>
    public void Configure(string workspaceKey, string channelId, IReadOnlyDictionary<string, int> quotas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentNullException.ThrowIfNull(quotas);

        foreach (var key in _limits.Keys.Where(k => k.Matches(workspaceKey, channelId)).ToList())
        {
            _limits.TryRemove(key, out _);
        }

        foreach (var (origin, limit) in quotas)
        {
            _limits[new CallQuotaKey(workspaceKey, channelId, NormalizeOrigin(origin))] = limit;
        }
    }

    /// <summary>
    /// Claims one line for <paramref name="origin"/>, or returns <see langword="null"/> when its quota
    /// is exhausted. Dispose the reservation to give the line back.
    /// </summary>
    public IDisposable? TryReserve(string workspaceKey, string channelId, string origin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);

        var key = new CallQuotaKey(workspaceKey, channelId, NormalizeOrigin(origin));
        if (!_limits.TryGetValue(key, out var limit))
        {
            return new CallQuotaReservation(this, key, counted: false);
        }

        // Claim first, roll back if it overshot. Checking and then incrementing lets every concurrent
        // caller pass the check while the count is still low — twenty dials at once is a Monday
        // morning, not a stress test.
        if (_inUse.AddOrUpdate(key, 1, (_, current) => current + 1) > limit)
        {
            _inUse.AddOrUpdate(key, 0, (_, current) => current - 1);
            return null;
        }

        return new CallQuotaReservation(this, key, counted: true);
    }

    /// <summary>
    /// Brings an origin into the form it is matched by. A telephone number is reduced to its digits,
    /// because an operator writes it the way their provider prints it and the trunk delivers it the
    /// way the network happens to. Anything else — <c>crm</c>, <c>dialer:campaign-x</c> — is a name a
    /// plugin passes and is matched as written; reducing it to digits would leave nothing at all.
    /// </summary>
    private static string NormalizeOrigin(string origin) =>
        PhoneNumberFormat.IsPhoneNumber(origin) ? PhoneNumberFormat.Normalize(origin) : origin.Trim();

    /// <summary>Gives a claimed line back. Called by the reservation, once.</summary>
    internal void Release(CallQuotaKey key) => _inUse.AddOrUpdate(key, 0, (_, current) => current - 1);
}
