using Callora.Plugin.Communication.Domain.Calls;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Workspace-scoped persistence port for <see cref="CallLog"/> (call-history metadata).
/// <para>
/// The write methods take an optional <see cref="CallEventOutboxEntry"/> and must persist it in
/// the <em>same</em> transaction as the log change (#113). That is what makes the outbox
/// transactional: an event can never describe a state the database does not hold, and a bus
/// outage cannot lose one.
/// </para>
/// </summary>
public interface ICallLogStore
{
    /// <summary>Persists a new call-history record together with its event, atomically.</summary>
    Task AddAsync(
        CallLog log,
        CallEventOutboxEntry? outboxEntry = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing record together with its event, atomically.
    /// </summary>
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException">
    /// The row changed since it was read. Concurrent provider callbacks for one call are
    /// serialized in the application, so this signals a second writer and the caller must
    /// re-read rather than overwrite.
    /// </exception>
    Task UpdateAsync(
        CallLog log,
        CallEventOutboxEntry? outboxEntry = null,
        CancellationToken cancellationToken = default);

    /// <summary>Lists the most recent records of a workspace, newest first.</summary>
    Task<IReadOnlyList<CallLog>> ListRecentAsync(string workspaceKey, int limit, CancellationToken cancellationToken = default);

    /// <summary>Deletes all records of a workspace (used by the GDPR purge contributor). Returns the count.</summary>
    Task<int> DeleteByWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes finished calls that ended before <paramref name="cutoff"/>, bounding how long a
    /// remote party's number is kept (#117). In-progress calls are never touched. Returns the
    /// count; idempotent, because a repeated run with the same cutoff finds nothing.
    /// </summary>
    Task<int> PurgeEndedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}
