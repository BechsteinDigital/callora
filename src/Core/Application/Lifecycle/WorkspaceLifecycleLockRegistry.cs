using System.Collections.Concurrent;

namespace Callora.Core.Application.Lifecycle;

/// <summary>
/// Provides reference-counted async locks scoped to one workspace/plugin pair.
/// </summary>
public sealed class WorkspaceLifecycleLockRegistry
{
    private readonly ConcurrentDictionary<string, WorkspaceLifecycleLock> _locks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Number of currently tracked lock entries.
    /// </summary>
    public int Count => _locks.Count;

    /// <summary>
    /// Builds the canonical lock key for one workspace/plugin pair.
    /// </summary>
    public static string BuildKey(string pluginId, string workspaceKey) =>
        $"{workspaceKey}:{pluginId}";

    /// <summary>
    /// Acquires the lock for one key, creating it on demand.
    /// </summary>
    public async Task<WorkspaceLifecycleLock> AcquireAsync(string lockKey, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = _locks.GetOrAdd(lockKey, _ => new WorkspaceLifecycleLock());
            Interlocked.Increment(ref candidate.ReferenceCount);

            if (_locks.TryGetValue(lockKey, out var current) &&
                ReferenceEquals(candidate, current))
            {
                await candidate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                return candidate;
            }

            if (Interlocked.Decrement(ref candidate.ReferenceCount) == 0)
            {
                _locks.TryRemove(new KeyValuePair<string, WorkspaceLifecycleLock>(lockKey, candidate));
            }
        }
    }

    /// <summary>
    /// Releases one previously acquired lock and removes it when unused.
    /// </summary>
    public void Release(string lockKey, WorkspaceLifecycleLock lockState)
    {
        lockState.Semaphore.Release();
        if (Interlocked.Decrement(ref lockState.ReferenceCount) == 0)
        {
            _locks.TryRemove(new KeyValuePair<string, WorkspaceLifecycleLock>(lockKey, lockState));
        }
    }
}
