using System.Text.Json;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Jobs.Contracts;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Delivers pending call events from the outbox to the business-event bus (#113).
/// <para>
/// Publishing used to be a best-effort side effect: a failing bus was logged and the event was
/// gone, which no consumer driving billing or CRM state can be built on. Delivery is now retried
/// with backoff until it succeeds, and each entry carries a stable id a consumer can deduplicate
/// on, because a retry after an ambiguous failure delivers the same event again.
/// </para>
/// </summary>
public sealed class CallEventOutboxDrainJobHandler(
    ICallEventOutbox outbox,
    IBusinessEventBus eventBus,
    TimeProvider timeProvider,
    ILogger<CallEventOutboxDrainJobHandler> logger) : IBackgroundJobHandler
{
    /// <summary>Job type key this handler is registered under.</summary>
    public const string JobTypeName = "communication.call-event-outbox-drain";

    /// <summary>Entries taken per run, so one drain cannot monopolise a worker.</summary>
    public const int BatchSize = 200;

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(15);

    /// <summary>How long a delivered entry is kept as evidence before it is purged.</summary>
    public static readonly TimeSpan DeliveredRetention = TimeSpan.FromDays(7);

    /// <inheritdoc />
    public string JobType => JobTypeName;

    /// <inheritdoc />
    public async Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var due = await outbox.ListDueAsync(now, BatchSize, cancellationToken).ConfigureAwait(false);

        foreach (var entry in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(entry.PayloadJson, PayloadOptions)
                    ?? [];
                await eventBus
                    .PublishAsync(new OutboxBusinessEvent(entry.EventName, entry.WorkspaceKey, data), cancellationToken)
                    .ConfigureAwait(false);
                entry.MarkDelivered(timeProvider.GetUtcNow());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A broken consumer must not stall the rest of the batch, so the failure is
                // recorded on this entry and the loop continues.
                entry.MarkFailed(timeProvider.GetUtcNow(), ex.Message, BaseRetryDelay, MaxRetryDelay);
                logger.LogWarning(
                    ex,
                    "Delivering call event {EventName} ({EntryId}) failed; attempt {Attempts}.",
                    entry.EventName,
                    entry.Id,
                    entry.Attempts);
            }

            await outbox.SaveAttemptAsync(entry, cancellationToken).ConfigureAwait(false);
        }

        await outbox.PurgeDeliveredAsync(now, DeliveredRetention, cancellationToken).ConfigureAwait(false);
    }
}
