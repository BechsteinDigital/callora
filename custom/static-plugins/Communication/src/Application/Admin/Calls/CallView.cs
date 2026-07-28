using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// Operator-facing view of one live call. Enum values are projected to strings so the REST shape is
/// stable JSON (the host serializes payloads with default options, which would emit enum numbers).
/// </summary>
/// <param name="CallId">Stable call identifier.</param>
/// <param name="Direction">Call direction (<c>Outbound</c>/<c>Inbound</c>).</param>
/// <param name="State">Lifecycle state (<c>Connecting</c>/<c>Ringing</c>/<c>Connected</c>/<c>Terminated</c>).</param>
/// <param name="Target">Remote participant address.</param>
public sealed record CallView(
    string CallId,
    string Direction,
    string State,
    string Target)
{
    /// <summary>Projects a live-call snapshot to its operator view.</summary>
    public static CallView From(CallSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new CallView(snapshot.CallId, snapshot.Direction.ToString(), snapshot.State.ToString(), snapshot.Target);
    }
}
