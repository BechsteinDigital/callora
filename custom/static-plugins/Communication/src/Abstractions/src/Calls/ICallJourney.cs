namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Where everything that happens to a call is written down, by whoever does it.
/// </summary>
/// <remarks>
/// <para>Every consumer that touches a call contributes its own steps. That is the point: no single
/// component knows the whole journey, and the operator's question — "what happened to this call" —
/// spans all of them.</para>
/// <para><b>Recording never blocks and never throws.</b> It runs on the path that is handling a live
/// call, where a slow write is a silence the caller hears and an exception is a dropped call. Steps
/// are kept with the call and written onto its history record when it ends.</para>
/// </remarks>
public interface ICallJourney
{
    /// <summary>Writes down one step of a call.</summary>
    /// <param name="workspaceKey">Workspace the call belongs to.</param>
    /// <param name="callId">The call. Unknown ids are accepted — a step may arrive before anything else.</param>
    /// <param name="step">What happened.</param>
    void Record(string workspaceKey, string callId, CallJourneyStep step);

    /// <summary>
    /// Reads a running call's steps so far. A call that has ended has its journey on its history
    /// record instead.
    /// </summary>
    IReadOnlyList<CallJourneyStep> Read(string workspaceKey, string callId);
}
