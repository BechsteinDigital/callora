using Callora.Plugin.Communication.Domain.Calls;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Drain side of the call-event outbox (#113). The write side lives on
/// <see cref="ICallLogStore"/>, because an entry is only correct when it is written in the same
/// transaction as the call-log change that produced it.
/// </summary>
public interface ICallEventOutbox
{
    /// <summary>
    /// Entries due for delivery at <paramref name="now"/>, oldest first so a workspace's events
    /// arrive in the order they happened.
    /// </summary>
    Task<IReadOnlyList<CallEventOutboxEntry>> ListDueAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Records the outcome of one delivery attempt.</summary>
    Task SaveAttemptAsync(CallEventOutboxEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes delivered entries older than <paramref name="retention"/>. Keeping them briefly
    /// lets an operator confirm a delivery after the fact; keeping them forever would grow the
    /// table with every call.
    /// </summary>
    Task<int> PurgeDeliveredAsync(
        DateTimeOffset now,
        TimeSpan retention,
        CancellationToken cancellationToken = default);
}
