using Callora.Plugin.Communication.Domain.Calls;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>Workspace-scoped persistence port for <see cref="CallLog"/> (call-history metadata).</summary>
public interface ICallLogStore
{
    /// <summary>Persists a new call-history record.</summary>
    Task AddAsync(CallLog log, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing record (e.g. after finalization).</summary>
    Task UpdateAsync(CallLog log, CancellationToken cancellationToken = default);

    /// <summary>Lists the most recent records of a workspace, newest first.</summary>
    Task<IReadOnlyList<CallLog>> ListRecentAsync(string workspaceKey, int limit, CancellationToken cancellationToken = default);

    /// <summary>Deletes all records of a workspace (used by the GDPR purge contributor). Returns the count.</summary>
    Task<int> DeleteByWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default);
}
