using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Domain.Calls;

/// <summary>
/// History record for one call — metadata only (no audio/recording). Created when the call
/// starts ringing and finalized when it ends. <see cref="RemoteParty"/> is personal data
/// (pseudonymizable / purgeable via the workspace data-purge contributor).
/// </summary>
public sealed class CallLog
{
    private IReadOnlyList<CallJourneyStep>? _journey;

    private CallLog(
        Guid recordId,
        string id,
        string workspaceKey,
        string? accountId,
        CallDirection direction,
        string remoteParty,
        string localIdentity,
        string? handledBy,
        string? correlationId,
        DateTimeOffset startedAt)
    {
        RecordId = recordId;
        Id = id;
        WorkspaceKey = workspaceKey;
        AccountId = accountId;
        Direction = direction;
        RemoteParty = remoteParty;
        LocalIdentity = localIdentity;
        HandledBy = handledBy;
        CorrelationId = correlationId;
        StartedAt = startedAt;
        Outcome = CallOutcome.InProgress;
    }

    /// <summary>Begins a history record for a ringing/connecting call.</summary>
    public static CallLog Start(
        string id,
        string workspaceKey,
        string? accountId,
        CallDirection direction,
        string remoteParty,
        string localIdentity,
        string? handledBy,
        string? correlationId,
        DateTimeOffset startedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteParty);
        ArgumentException.ThrowIfNullOrWhiteSpace(localIdentity);

        return new CallLog(
            Guid.CreateVersion7(), id, workspaceKey, accountId, direction, remoteParty,
            localIdentity, handledBy, correlationId, startedAt);
    }

    /// <summary>
    /// Primary key of the history record, independent of any provider identifier (#113).
    /// <para>
    /// <see cref="Id"/> used to be the key, but a provider's call id is unique only inside its
    /// own channel: two channels reporting the same id collided on insert, so the second, entirely
    /// legitimate call could not be recorded. Version 7 so the key sorts by creation time and
    /// index inserts stay sequential.
    /// </para>
    /// </summary>
    public Guid RecordId { get; }

    /// <summary>
    /// The provider's call id. Unique within its channel, not globally, which is why it is no
    /// longer the primary key. This is the id consumers name in call-control calls.
    /// </summary>
    public string Id { get; }

    /// <summary>Owning workspace.</summary>
    public string WorkspaceKey { get; }

    /// <summary>Account the call ran on (when known).</summary>
    public string? AccountId { get; }

    /// <summary>Call direction.</summary>
    public CallDirection Direction { get; }

    /// <summary>Remote participant (personal data — pseudonymizable).</summary>
    public string RemoteParty { get; private set; }

    /// <summary>Local identity/number involved.</summary>
    public string LocalIdentity { get; }

    /// <summary>Consumer/plugin that handled the call (for example <c>ai-agent</c>).</summary>
    public string? HandledBy { get; }

    /// <summary>Correlation id linking the call across systems.</summary>
    public string? CorrelationId { get; }

    /// <summary>When the call started (ringing/connecting).</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>When the call was answered, if it was.</summary>
    public DateTimeOffset? AnsweredAt { get; private set; }

    /// <summary>When the call ended, once finalized.</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>Talk time in seconds (answered → ended); 0 when never answered.</summary>
    public int DurationSeconds { get; private set; }

    /// <summary>Terminal outcome; <see cref="CallOutcome.InProgress"/> until finalized.</summary>
    public CallOutcome Outcome { get; private set; }

    /// <summary>Protocol disconnect cause, when reported.</summary>
    public string? DisconnectCause { get; private set; }

    /// <summary>
    /// Marks the call answered — the only transition out of the in-progress state. Rejected once
    /// the call has ended or is already answered, and the answer time may not precede the start.
    /// </summary>
    public void MarkAnswered(DateTimeOffset answeredAt)
    {
        if (EndedAt is not null)
        {
            throw new InvalidOperationException($"Call '{Id}' has ended and can no longer be answered.");
        }

        if (AnsweredAt is not null)
        {
            throw new InvalidOperationException($"Call '{Id}' is already answered.");
        }

        if (answeredAt < StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(answeredAt), "The answer time cannot precede the start time.");
        }

        AnsweredAt = answeredAt;
    }

    /// <summary>
    /// Finalizes the call with a terminal outcome and computes the talk time. Rejects a second end,
    /// a non-terminal <see cref="CallOutcome.InProgress"/>, an outcome that contradicts whether the
    /// call was answered (a completed call must have been answered; missed/rejected/busy must not),
    /// and end times that precede the start or the answer.
    /// </summary>
    public void End(DateTimeOffset endedAt, CallOutcome outcome, string? disconnectCause)
    {
        if (EndedAt is not null)
        {
            throw new InvalidOperationException($"Call '{Id}' has already ended.");
        }

        if (outcome == CallOutcome.InProgress)
        {
            throw new ArgumentException("An ended call cannot be InProgress.", nameof(outcome));
        }

        var wasAnswered = AnsweredAt is not null;
        if (wasAnswered && !IsAnsweredOutcome(outcome))
        {
            throw new ArgumentException($"An answered call cannot end as {outcome}.", nameof(outcome));
        }

        if (!wasAnswered && !IsUnansweredOutcome(outcome))
        {
            throw new ArgumentException($"An unanswered call cannot end as {outcome}.", nameof(outcome));
        }

        if (endedAt < StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(endedAt), "The end time cannot precede the start time.");
        }

        if (wasAnswered && endedAt < AnsweredAt!.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(endedAt), "The end time cannot precede the answer time.");
        }

        EndedAt = endedAt;
        Outcome = outcome;
        DisconnectCause = disconnectCause;
        DurationSeconds = wasAnswered ? (int)(endedAt - AnsweredAt!.Value).TotalSeconds : 0;
    }

    /// <summary>
    /// What happened to the call, in order, as its participants recorded it. Written once when the
    /// call ends: a history row says a call ended, this says why it went where it went.
    /// </summary>
    /// <remarks>
    /// Reads as empty when nothing was materialized — a NULL column bypasses the value converter, so
    /// EF hands the field null rather than the empty list the converter would produce. Every row that
    /// predates this column is in exactly that state.
    /// </remarks>
    public IReadOnlyList<CallJourneyStep> Journey
    {
        get => _journey ?? [];
        private set => _journey = value;
    }

    /// <summary>
    /// Attaches the call's steps to its record. Called once, where the call is finalized — replacing
    /// rather than appending, because the buffer holds the whole story and hands it over as one.
    /// </summary>
    public void RecordJourney(IReadOnlyList<CallJourneyStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        Journey = steps;
    }

    /// <summary>Outcomes a call that was answered may end with.</summary>
    private static bool IsAnsweredOutcome(CallOutcome outcome) =>
        outcome is CallOutcome.Completed or CallOutcome.Failed or CallOutcome.Interrupted;

    /// <summary>Outcomes a call that was never answered may end with.</summary>
    /// <remarks>
    /// <see cref="CallOutcome.Interrupted"/> is the one outcome valid on both sides, and for a
    /// reason: it says nothing about the conversation, only that the host went away. A call can be
    /// cut short mid-sentence or while it was still ringing, and both are the same event.
    /// </remarks>
    private static bool IsUnansweredOutcome(CallOutcome outcome) =>
        outcome is CallOutcome.Missed or CallOutcome.Rejected or CallOutcome.Busy
            or CallOutcome.NoAnswer or CallOutcome.Canceled or CallOutcome.Failed
            or CallOutcome.Interrupted;

    /// <summary>Replaces the remote party with a pseudonym for GDPR erasure.</summary>
    public void Pseudonymize(string pseudonym)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pseudonym);
        RemoteParty = pseudonym;
    }
}
