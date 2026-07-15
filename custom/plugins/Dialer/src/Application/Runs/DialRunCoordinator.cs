using System.Text.Json;
using Callora.Core.Application.Jobs.Contracts;

namespace Callora.Plugins.Dialer.Application.Runs;

/// <summary>
/// Starts dial runs as durable background jobs and answers status queries
/// from the persisted snapshots.
/// </summary>
public sealed class DialRunCoordinator(
    DataStoreDialRunStore runStore,
    IBackgroundJobQueue jobQueue)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Starts one run for the workspace. Returns null while a run is in progress.
    /// </summary>
    public async Task<DialRunSnapshot?> StartRunAsync(
        string workspaceKey,
        DialRunOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentNullException.ThrowIfNull(options);
        var normalizedKey = workspaceKey.Trim();

        var latest = await runStore.GetLatestAsync(normalizedKey, cancellationToken).ConfigureAwait(false);
        if (latest is { Status: DialRunStatus.Running })
        {
            return null;
        }

        var snapshot = new DialRunSnapshot(
            RunId: Guid.NewGuid().ToString("N"),
            WorkspaceKey: normalizedKey,
            Status: DialRunStatus.Running,
            Attempts: Array.Empty<DialAttemptResult>(),
            ErrorMessage: null,
            StartedAtUtc: DateTimeOffset.UtcNow,
            CompletedAtUtc: null);

        await runStore.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);

        var payload = new DialRunJobPayload(
            snapshot.RunId,
            normalizedKey,
            (int)Math.Ceiling(options.CallTimeout.TotalSeconds));

        await jobQueue.EnqueueAsync(
            new BackgroundJobRequest(
                JobType: DialRunJobHandler.JobTypeName,
                PayloadJson: JsonSerializer.Serialize(payload, JsonOptions),
                MaxAttempts: 1,
                WorkspaceKey: normalizedKey),
            cancellationToken).ConfigureAwait(false);

        return snapshot;
    }

    /// <summary>
    /// Returns the latest run of one workspace, or null when none was started.
    /// </summary>
    public Task<DialRunSnapshot?> GetLatestRunAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
            return Task.FromResult<DialRunSnapshot?>(null);

        return runStore.GetLatestAsync(workspaceKey.Trim(), cancellationToken);
    }
}
