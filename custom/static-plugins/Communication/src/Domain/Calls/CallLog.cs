using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Domain.Calls;

/// <summary>
/// History record for one call — metadata only (no audio/recording). Created when the call
/// starts ringing and finalized when it ends. <see cref="RemoteParty"/> is personal data
/// (pseudonymizable / purgeable via the workspace data-purge contributor).
/// </summary>
public sealed class CallLog
{
    private CallLog(
        string id,
        string workspaceKey,
        string? accountId,
        string? lineId,
        CallDirection direction,
        string remoteParty,
        string localIdentity,
        string? handledBy,
        string? correlationId,
        DateTimeOffset startedAt)
    {
        Id = id;
        WorkspaceKey = workspaceKey;
        AccountId = accountId;
        LineId = lineId;
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
        string? lineId,
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
            id, workspaceKey, accountId, lineId, direction, remoteParty, localIdentity,
            handledBy, correlationId, startedAt);
    }

    /// <summary>Stable identifier.</summary>
    public string Id { get; }

    /// <summary>Owning workspace.</summary>
    public string WorkspaceKey { get; }

    /// <summary>Account the call ran on (when known).</summary>
    public string? AccountId { get; }

    /// <summary>Line the call ran on (when known).</summary>
    public string? LineId { get; }

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

    /// <summary>Records that the call was answered.</summary>
    public void MarkAnswered(DateTimeOffset answeredAt)
    {
        AnsweredAt ??= answeredAt;
    }

    /// <summary>Ends the record with a terminal outcome and computes the talk time.</summary>
    public void End(DateTimeOffset endedAt, CallOutcome outcome, string? disconnectCause)
    {
        if (outcome == CallOutcome.InProgress)
        {
            throw new ArgumentException("An ended call cannot be InProgress.", nameof(outcome));
        }

        EndedAt = endedAt;
        Outcome = outcome;
        DisconnectCause = disconnectCause;
        DurationSeconds = AnsweredAt is { } answered && endedAt > answered
            ? (int)(endedAt - answered).TotalSeconds
            : 0;
    }

    /// <summary>Replaces the remote party with a pseudonym for GDPR erasure.</summary>
    public void Pseudonymize(string pseudonym)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pseudonym);
        RemoteParty = pseudonym;
    }
}
