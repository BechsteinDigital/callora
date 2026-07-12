using System.Collections.Concurrent;
using Callora.Plugins.Dialer.Application.Numbers;

namespace Callora.Plugins.Dialer.Application.Runs;

/// <summary>
/// Starts dial runs in the background and tracks the latest run per workspace.
/// </summary>
public sealed class DialRunTracker(
    DialRunExecutor executor,
    IDialNumberStore numberStore) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, DialRunSnapshot> _latestRunByWorkspace = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _shutdown = new();

    /// <summary>
    /// Starts one run for the workspace. Returns null when a run is already in progress.
    /// </summary>
    public async Task<DialRunSnapshot?> StartRunAsync(
        string workspaceKey,
        DialRunOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        var normalizedKey = workspaceKey.Trim();

        var numbers = await numberStore.ListAsync(normalizedKey, cancellationToken).ConfigureAwait(false);
        var snapshot = new DialRunSnapshot(
            RunId: Guid.NewGuid().ToString("N"),
            WorkspaceKey: normalizedKey,
            Status: DialRunStatus.Running,
            Attempts: Array.Empty<DialAttemptResult>(),
            ErrorMessage: null,
            StartedAtUtc: DateTimeOffset.UtcNow,
            CompletedAtUtc: null);

        var accepted = true;
        _latestRunByWorkspace.AddOrUpdate(
            normalizedKey,
            snapshot,
            (_, current) =>
            {
                if (current.Status == DialRunStatus.Running)
                {
                    accepted = false;
                    return current;
                }

                return snapshot;
            });

        if (!accepted)
            return null;

        _ = RunInBackgroundAsync(snapshot, numbers, options);
        return snapshot;
    }

    /// <summary>
    /// Returns the latest run of one workspace, or null when none was started.
    /// </summary>
    public DialRunSnapshot? GetLatestRun(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
            return null;

        return _latestRunByWorkspace.TryGetValue(workspaceKey.Trim(), out var snapshot) ? snapshot : null;
    }

    /// <summary>
    /// Awaits completion of the latest run; intended for tests and graceful shutdown.
    /// </summary>
    public async Task<DialRunSnapshot?> WaitForCompletionAsync(
        string workspaceKey,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = GetLatestRun(workspaceKey);
            if (snapshot is not null && snapshot.Status != DialRunStatus.Running)
                return snapshot;

            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken).ConfigureAwait(false);
        }

        return GetLatestRun(workspaceKey);
    }

    public ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        _shutdown.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task RunInBackgroundAsync(
        DialRunSnapshot snapshot,
        IReadOnlyList<DialNumberEntry> numbers,
        DialRunOptions options)
    {
        DialRunSnapshot completed;
        try
        {
            var attempts = await executor
                .ExecuteAsync(snapshot.WorkspaceKey, numbers, options, _shutdown.Token)
                .ConfigureAwait(false);

            completed = snapshot with
            {
                Status = DialRunStatus.Completed,
                Attempts = attempts,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            completed = snapshot with
            {
                Status = DialRunStatus.Failed,
                ErrorMessage = ex.Message,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };
        }

        _latestRunByWorkspace[snapshot.WorkspaceKey] = completed;
    }
}
