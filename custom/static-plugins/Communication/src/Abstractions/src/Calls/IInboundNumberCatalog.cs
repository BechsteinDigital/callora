namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Which numbers can reach a workspace at all.
/// </summary>
/// <remarks>
/// <para>A consumer that owns particular inbound calls has to name the numbers it answers (see
/// <see cref="ICall.InboundIdentity"/>), and letting an operator type them from memory is how a line
/// ends up listening for a number no trunk ever delivers — the number is right, the punctuation or the
/// national form is not, and nothing rings. This turns that free-text field into a choice.</para>
/// <para>It says what <em>can</em> arrive, not who gets it. Routing stays where it is: owners decide
/// for themselves and the first one that claims a call gets it.</para>
/// </remarks>
public interface IInboundNumberCatalog
{
    /// <summary>
    /// Lists the numbers configured on the workspace's lines. A trunk that accepts every number
    /// contributes nothing — it cannot say which numbers those would be.
    /// </summary>
    /// <param name="workspaceKey">The workspace to look in.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    Task<IReadOnlyList<InboundNumber>> ListAsync(string workspaceKey, CancellationToken cancellationToken = default);
}
