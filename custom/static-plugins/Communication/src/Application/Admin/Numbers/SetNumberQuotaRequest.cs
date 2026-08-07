namespace Callora.Plugin.Communication.Application.Admin.Numbers;

/// <summary>Body of <c>POST numbers/quota</c>.</summary>
/// <param name="ChannelId">The line the number arrives on.</param>
/// <param name="Number">The number, in any usual spelling.</param>
/// <param name="MaxConcurrentCalls">
/// How many of the line's calls the number may hold at once, or <see langword="null"/> to remove the
/// limit. There is no zero: no lines is not a quota but a ban, and for that there is the account.
/// </param>
public sealed record SetNumberQuotaRequest(string? ChannelId, string? Number, int? MaxConcurrentCalls);
