namespace Callora.Plugin.Communication.Application.Admin.Numbers;

/// <summary>
/// One number a workspace can be reached on, and everything an operator asks about it in one row.
/// </summary>
/// <remarks>
/// Setting up "the support number, at most five lines, goes to the conference" used to mean two
/// plugins and a screen that did not exist: the line's numbers live on the account, the quota lives in
/// a field of that account nothing rendered, and what actually arrived lived only in the call history.
/// </remarks>
/// <param name="Number">The number, as the line reports it.</param>
/// <param name="ChannelId">The line that delivers it.</param>
/// <param name="ChannelDisplayName">Operator-facing name of that line.</param>
/// <param name="MaxConcurrentCalls">
/// How many of the line's calls this number may hold at once, or <see langword="null"/> for
/// unlimited. Null rather than zero: no configuration means no split, not a ban.
/// </param>
/// <param name="RecentCalls">
/// How many of the recent calls arrived on this number. The first question anybody asks about a
/// shared trunk is whether anything reached this number at all.
/// </param>
/// <param name="LastCallAt">When the most recent of them started, or null when there were none.</param>
public sealed record NumberPlanEntry(
    string Number,
    string ChannelId,
    string ChannelDisplayName,
    int? MaxConcurrentCalls,
    int RecentCalls,
    DateTimeOffset? LastCallAt);
