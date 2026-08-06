namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Resolves a running call by its identifier, handing out the live <see cref="ICall"/> rather than a
/// snapshot — the seam a consumer needs to observe a call as it happens instead of polling it.
/// </summary>
/// <remarks>
/// <para><b>Why this exists next to <see cref="ICallControlService"/>.</b> That contract deliberately
/// speaks in <see cref="CallSnapshot"/> DTOs: it commands calls and reports their state, and a DTO is
/// the right currency for both. But a snapshot cannot carry an event, so anything that has to react to
/// a call as it runs — collecting DTMF digits, processing its audio, bridging it somewhere — had no way
/// in. This contract is that way in, and it is deliberately narrow: resolve the call, then use the
/// <see cref="ICall"/> surface.</para>
/// <para><b>Workspace-scoped by construction.</b> A call is only ever resolved for the workspace that
/// owns it; naming another workspace's call id yields <see langword="null"/>, never the call.</para>
/// <para><b>The handle is borrowed, not owned.</b> The returned call belongs to the communication
/// plugin, which disposes it when the call ends. A consumer must not hold it past
/// <see cref="CallState.Terminated"/>, and must detach its handlers when the call terminates —
/// otherwise it keeps a dead call alive.</para>
/// </remarks>
public interface ICallAccess
{
    /// <summary>
    /// Returns the live call <paramref name="workspaceKey"/> owns under <paramref name="callId"/>, or
    /// <see langword="null"/> when that workspace has no such active call — including when another
    /// workspace does.
    /// </summary>
    /// <param name="workspaceKey">The workspace the caller acts for.</param>
    /// <param name="callId">The call's identifier, as reported on <see cref="CallSnapshot.CallId"/>.</param>
    ICall? Find(string workspaceKey, string callId);
}
