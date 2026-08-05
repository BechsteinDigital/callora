using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Voice;

/// <summary>
/// Writes a connectivity transition observed on a live channel back onto the persisted
/// account (#112).
/// <para>
/// <c>SipAccount.ReportStatus</c> existed but no production code called it, so an account
/// stayed on <see cref="SipAccountStatus.Connecting"/> forever and a lost registration was
/// visible only in the log. This port is the seam that closes that loop, and keeps the
/// reconciler free of persistence.
/// </para>
/// </summary>
public interface ISipAccountStatusProjector
{
    /// <summary>
    /// Records the account's new connectivity status. Must not throw: it runs on a provider
    /// callback, where a persistence failure has to be logged and swallowed rather than tear
    /// down the channel that just reported healthy.
    /// </summary>
    /// <param name="error">
    /// Reason for a failed or degraded transition. Redaction happens in the domain, so a caller
    /// may pass a raw provider message.
    /// </param>
    Task ProjectAsync(
        string workspaceKey,
        string accountId,
        SipAccountStatus status,
        string? error,
        CancellationToken cancellationToken = default);
}
