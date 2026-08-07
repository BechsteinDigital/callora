namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Divides a trunk's lines between the things that use it, so one consumer working through a queue
/// cannot take the lines another needs to answer with.
/// </summary>
/// <remarks>
/// <para>The account's concurrent-call limit already stops everyone at the ceiling. Quotas decide who
/// reaches it first — the case that matters is a dialer against an agent, because the dialer never
/// waits and the agent can only wait.</para>
/// <para>Origins are named by whoever places the call (<see cref="PlaceCallCommand.Origin"/>):
/// <c>crm</c>, or <c>dialer:campaign-x</c> when one consumer runs several things that should not
/// exhaust each other. An origin nobody configured is unlimited — splitting a trunk is deliberate, and
/// a silent limit of zero would be the most hostile reading of an empty configuration.</para>
/// </remarks>
public interface ICallQuotaRegistry
{
    /// <summary>
    /// Sets the quotas for one channel's lines, replacing whatever was configured before. Quotas may
    /// add up to more than the channel has: dividing exactly would leave lines idle whenever an origin
    /// is quiet, and the account limit remains the real ceiling.
    /// </summary>
    /// <param name="workspaceKey">The workspace whose trunk is being divided.</param>
    /// <param name="channelId">The channel carrying the lines.</param>
    /// <param name="quotas">Origin name to its maximum simultaneous calls.</param>
    void Configure(string workspaceKey, string channelId, IReadOnlyDictionary<string, int> quotas);
}
