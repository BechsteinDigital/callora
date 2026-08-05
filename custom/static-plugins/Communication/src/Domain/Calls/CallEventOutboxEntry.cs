namespace Callora.Plugin.Communication.Domain.Calls;

/// <summary>
/// One call event awaiting delivery (#113). Written in the same transaction as the call-log
/// change that produced it, so an event can never describe a state the database does not hold,
/// and a bus outage can never lose it.
/// <para>
/// Before this, publishing was best effort: a failing bus was logged and the event was gone.
/// Consumers that drive billing or CRM state cannot be built on that.
/// </para>
/// </summary>
public sealed class CallEventOutboxEntry
{
    private CallEventOutboxEntry(
        Guid id,
        string eventName,
        string workspaceKey,
        string payloadJson,
        DateTimeOffset occurredAt)
    {
        Id = id;
        EventName = eventName;
        WorkspaceKey = workspaceKey;
        PayloadJson = payloadJson;
        OccurredAt = occurredAt;
        NextAttemptAt = occurredAt;
    }

#pragma warning disable CS8618 // Materialization seam: EF sets the properties after construction.
    private CallEventOutboxEntry()
    {
    }
#pragma warning restore CS8618

    /// <summary>
    /// Delivery identity. Doubles as the idempotency key a consumer can deduplicate on, because
    /// a retry after an ambiguous failure delivers the same id again.
    /// </summary>
    public Guid Id { get; }

    /// <summary>Business-event name, for example <c>call.ringing</c>.</summary>
    public string EventName { get; }

    /// <summary>Workspace the event belongs to.</summary>
    public string WorkspaceKey { get; }

    /// <summary>Serialized event data, exactly as the event would have carried it.</summary>
    public string PayloadJson { get; }

    /// <summary>When the transition happened, which is not when delivery is attempted.</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Delivery attempts so far.</summary>
    public int Attempts { get; private set; }

    /// <summary>Earliest next attempt; moves out with backoff after each failure.</summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    /// <summary>When delivery succeeded. Null while pending.</summary>
    public DateTimeOffset? DeliveredAt { get; private set; }

    /// <summary>Reason for the last failed attempt, for operator diagnosis.</summary>
    public string? LastError { get; private set; }

    /// <summary>Creates a pending entry for one business event.</summary>
    public static CallEventOutboxEntry Pending(
        Guid id,
        string eventName,
        string workspaceKey,
        string payloadJson,
        DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        return new CallEventOutboxEntry(id, eventName, workspaceKey, payloadJson, occurredAt);
    }

    /// <summary>Marks the entry delivered. Idempotent, so a duplicate drain is harmless.</summary>
    public void MarkDelivered(DateTimeOffset at)
    {
        if (DeliveredAt is not null)
        {
            return;
        }

        DeliveredAt = at;
        LastError = null;
        Attempts++;
    }

    /// <summary>
    /// Records a failed attempt and schedules the next one with exponential backoff, capped so a
    /// long-broken consumer is still retried on a predictable cadence rather than effectively
    /// never.
    /// </summary>
    public void MarkFailed(DateTimeOffset at, string? error, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        Attempts++;
        LastError = error is { Length: > 500 } ? error[..500] : error;

        var exponent = Math.Min(Attempts, 10);
        var delayTicks = Math.Min(baseDelay.Ticks * (1L << (exponent - 1)), maxDelay.Ticks);
        NextAttemptAt = at.AddTicks(delayTicks);
    }
}
