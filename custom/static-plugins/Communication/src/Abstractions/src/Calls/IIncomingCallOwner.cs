namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Claims responsibility for inbound calls — the consumer that decides whether a call is answered,
/// rejected, or put somewhere.
/// </summary>
/// <remarks>
/// <para><b>Owning is not observing.</b> A recorder, a wallboard, an analytics consumer or a
/// compliance archive wants to see calls, not decide about them; those use the live call event stream,
/// which carries snapshots and no way to act. Keeping the two apart is what stops a dashboard from
/// being one careless line away from answering somebody's customer.</para>
/// <para><b>The first owner that claims a call gets it.</b> Offering it onwards afterwards would hand
/// the same call to two owners, and the second would act on something already being answered. An owner
/// that declines is passed over, so a consumer responsible for a subset can say so instead of having
/// to answer everything.</para>
/// <para>An owner is asked on the path that received the call. It should decide quickly — accepting or
/// rejecting can follow afterwards; what must not happen is a long lookup while the caller listens to
/// silence.</para>
/// </remarks>
public interface IIncomingCallOwner
{
    /// <summary>
    /// Who this owner is, for the record of what happened to a call. Defaults to
    /// <see cref="CallOwnerIdentity.Anonymous"/> so an owner written before identities existed keeps
    /// working — and is reported as unnamed rather than as something invented.
    /// </summary>
    CallOwnerIdentity Identity => CallOwnerIdentity.Anonymous;

    /// <summary>
    /// Offers one inbound call. Return <see langword="true"/> to take responsibility for it,
    /// <see langword="false"/> to let it pass to the next owner.
    /// </summary>
    /// <param name="workspaceKey">The workspace the call arrived in.</param>
    /// <param name="call">The ringing call.</param>
    /// <param name="cancellationToken">Cancels the decision.</param>
    Task<bool> TryClaimAsync(string workspaceKey, ICall call, CancellationToken cancellationToken = default);
}
