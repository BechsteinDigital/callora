namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>
/// One line share as an operator sends it. Every field is optional so a typo becomes a 400 the
/// operator can act on, rather than an exception out of the domain.
/// </summary>
/// <param name="Origin">
/// What is claiming the lines, as the calling plugin names it — <c>crm</c>, or a finer
/// <c>dialer:campaign-x</c> when one consumer runs several things that should not exhaust each other.
/// </param>
/// <param name="MaxConcurrentCalls">Lines that origin may hold at once; at least one.</param>
public sealed record CallQuotaRequest(string? Origin, int? MaxConcurrentCalls);
