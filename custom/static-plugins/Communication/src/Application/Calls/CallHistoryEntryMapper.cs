using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Calls;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Projects a persisted <see cref="CallLog"/> to its read-only <see cref="CallHistoryEntry"/> view.
/// Lives in the implementation (not the contract) because it depends on the domain <see cref="CallLog"/>,
/// which the Abstractions package must not reference.
/// </summary>
internal static class CallHistoryEntryMapper
{
    /// <summary>Projects a persisted <see cref="CallLog"/> to its read-only history view.</summary>
    public static CallHistoryEntry FromDomain(CallLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        return new CallHistoryEntry(
            log.Id,
            log.Direction.ToString(),
            log.RemoteParty,
            log.LocalIdentity,
            log.StartedAt,
            log.AnsweredAt,
            log.EndedAt,
            log.DurationSeconds,
            log.Outcome.ToString(),
            log.DisconnectCause,
            log.Journey);
    }
}
