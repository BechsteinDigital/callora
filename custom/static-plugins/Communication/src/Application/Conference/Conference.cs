namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// In-memory SFU state for a single conference: the participant set behind a per-conference lock. Topology
/// mutations (join/leave) take <see cref="Gate"/>; the frame-forwarding path never takes the lock — it
/// reads a point-in-time snapshot via <see cref="Snapshot"/> so a synchronous media receive callback never
/// blocks on a concurrent join/leave.
/// </summary>
internal sealed class Conference
{
    private readonly Dictionary<string, ConferenceParticipantEntry> _participants = new(StringComparer.Ordinal);

    /// <summary>Serializes topology mutations for this conference; never held across an <c>await</c> of a network send.</summary>
    public object Gate { get; } = new();

    /// <summary>The live participant map. Only mutate under <see cref="Gate"/>.</summary>
    public Dictionary<string, ConferenceParticipantEntry> Participants => _participants;

    /// <summary>
    /// A lock-free copy of the current participants for the forwarding path. Taking the copy under the lock
    /// keeps it internally consistent; the returned array is safe to read without the lock.
    /// </summary>
    public ConferenceParticipantEntry[] Snapshot()
    {
        lock (Gate)
        {
            return [.. _participants.Values];
        }
    }
}
