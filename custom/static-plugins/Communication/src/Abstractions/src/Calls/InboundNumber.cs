namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// One number that can reach this workspace, and the line it arrives on.
/// </summary>
/// <param name="Number">The number as the operator configured it on the account.</param>
/// <param name="ChannelId">
/// The channel that receives it — the same id a call reports and a quota is keyed by, so a consumer
/// can correlate the three without a second lookup.
/// </param>
/// <param name="ChannelDisplayName">
/// Operator-facing name of that line. Two trunks with consecutive number blocks are otherwise
/// indistinguishable in a list.
/// </param>
public sealed record InboundNumber(string Number, string ChannelId, string ChannelDisplayName);
