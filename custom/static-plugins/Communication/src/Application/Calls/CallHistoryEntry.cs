using Callora.Plugin.Communication.Domain.Calls;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Read-only history view of one recorded call. Enum values are projected to strings so the shape is
/// stable JSON for the REST adapter (the host serializes payloads with default options, which would
/// otherwise emit enum numbers). Consumed both in-process and over REST.
/// </summary>
/// <param name="CallId">Stable call identifier.</param>
/// <param name="Direction">Call direction (<c>Outbound</c>/<c>Inbound</c>).</param>
/// <param name="RemoteParty">Remote participant address (personal data).</param>
/// <param name="StartedAt">When the call started.</param>
/// <param name="AnsweredAt">When it was answered, if it was.</param>
/// <param name="EndedAt">When it ended, once finalized.</param>
/// <param name="DurationSeconds">Talk time in seconds (0 when never answered).</param>
/// <param name="Outcome">Terminal outcome (<c>InProgress</c> until finalized).</param>
/// <param name="DisconnectCause">Protocol disconnect cause, when reported.</param>
public sealed record CallHistoryEntry(
    string CallId,
    string Direction,
    string RemoteParty,
    DateTimeOffset StartedAt,
    DateTimeOffset? AnsweredAt,
    DateTimeOffset? EndedAt,
    int DurationSeconds,
    string Outcome,
    string? DisconnectCause)
{
    /// <summary>Projects a persisted <see cref="CallLog"/> to its read-only history view.</summary>
    public static CallHistoryEntry FromDomain(CallLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        return new CallHistoryEntry(
            log.Id,
            log.Direction.ToString(),
            log.RemoteParty,
            log.StartedAt,
            log.AnsweredAt,
            log.EndedAt,
            log.DurationSeconds,
            log.Outcome.ToString(),
            log.DisconnectCause);
    }
}
