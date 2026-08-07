namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Reading what calls happened, without the ability to act on any of them.
/// </summary>
/// <remarks>
/// The read half of <see cref="ICallControlService"/>, split out because the two are already
/// different rights — a workspace's permissions distinguish reading calls from managing them. A
/// report, a dashboard or a number plan needs the history and has no business being able to hang up.
/// </remarks>
public interface ICallHistory
{
    /// <summary>
    /// Returns the workspace's recent calls, newest first, capped at <paramref name="limit"/>.
    /// </summary>
    Task<IReadOnlyList<CallHistoryEntry>> ListRecentAsync(
        string workspaceKey,
        int limit,
        CancellationToken cancellationToken = default);
}
