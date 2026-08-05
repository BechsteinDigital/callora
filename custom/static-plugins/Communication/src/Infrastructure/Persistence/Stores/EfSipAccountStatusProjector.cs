using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Voice;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Stores;

/// <summary>
/// Persists a channel's connectivity transition onto its SIP account (#112).
/// <para>
/// Deliberately total: every failure is logged and swallowed. The caller is a provider
/// callback on the registration path, and a database hiccup there must not propagate into
/// the SIP stack or kill a channel that just came back up.
/// </para>
/// </summary>
public sealed class EfSipAccountStatusProjector(
    ISipAccountStore store,
    TimeProvider timeProvider,
    ILogger<EfSipAccountStatusProjector> logger) : ISipAccountStatusProjector
{
    /// <inheritdoc />
    public async Task ProjectAsync(
        string workspaceKey,
        string accountId,
        SipAccountStatus status,
        string? error,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var account = await store.GetAsync(workspaceKey, accountId, cancellationToken).ConfigureAwait(false);
            if (account is null)
            {
                // The account was deleted while its channel was still reporting; nothing to record.
                return;
            }

            account.ReportStatus(status, error, timeProvider.GetUtcNow());
            await store.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Persisting status {Status} for SIP account {AccountId} in workspace {WorkspaceKey} failed.",
                status,
                accountId,
                workspaceKey);
        }
    }
}
