using System.Text.Json;
using Callora.Core.Application.Jobs.Contracts;
using Callora.Plugins.Dialer.Application.Numbers;

namespace Callora.Plugins.Dialer.Application.Runs;

/// <summary>
/// Executes dial runs from the durable host job queue. The persisted snapshot
/// reflects the outcome; a throwing run additionally lands as dead letter in
/// the job monitoring.
/// </summary>
public sealed class DialRunJobHandler(
    DialRunExecutor executor,
    IDialNumberStore numberStore,
    DataStoreDialRunStore runStore) : IBackgroundJobHandler
{
    public const string JobTypeName = "dialer.run";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string JobType => JobTypeName;

    public async Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<DialRunJobPayload>(context.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Dial run job payload is empty.");

        var snapshot = await runStore
            .GetRunAsync(payload.WorkspaceKey, payload.RunId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Dial run '{payload.RunId}' was not found.");

        try
        {
            var numbers = await numberStore.ListAsync(payload.WorkspaceKey, cancellationToken).ConfigureAwait(false);
            var options = new DialRunOptions(TimeSpan.FromSeconds(Math.Max(1, payload.CallTimeoutSeconds)));
            var attempts = await executor
                .ExecuteAsync(payload.WorkspaceKey, numbers, options, cancellationToken)
                .ConfigureAwait(false);

            await runStore.SaveAsync(snapshot with
            {
                Status = DialRunStatus.Completed,
                Attempts = attempts,
                CompletedAtUtc = DateTimeOffset.UtcNow
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await runStore.SaveAsync(snapshot with
            {
                Status = DialRunStatus.Failed,
                ErrorMessage = ex.Message,
                CompletedAtUtc = DateTimeOffset.UtcNow
            }, CancellationToken.None).ConfigureAwait(false);

            // Erneut werfen: Der Job erscheint als Dead Letter in /api/jobs.
            throw;
        }
    }
}
