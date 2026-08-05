using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Voice;
using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>
/// Couples an admin mutation to the live runtime (#110): persist, reconcile, and let the
/// response tell the truth about both.
/// <para>
/// Before this existed the handlers only wrote rows, so a created account did not register
/// until the next restart and a disabled one kept taking calls. Now every successful write is
/// followed by a reconciliation, and a runtime failure is written back onto the account as a
/// <see cref="SipAccountStatus.Failed"/> status with its reason — so the persisted state and
/// the response agree with what the runtime actually did.
/// </para>
/// <para>
/// Deployments without a voice runtime (no SDK client configured) pass a null reconciler; the
/// handlers then behave as pure persistence, which is the honest behaviour for a host that
/// operates no channels.
/// </para>
/// </summary>
internal sealed class SipAccountRuntimeCoordinator(
    ISipAccountStore store,
    ISipAccountRuntimeReconciler? reconciler,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Reconciles <paramref name="account"/> after it was persisted. On failure the account's
    /// status is written back as failed and the caller receives a <c>502</c> carrying both the
    /// reason and the account, so an operator sees the configuration that exists <em>and</em>
    /// that it is not live. Returns null when the runtime matches the desired state.
    /// </summary>
    public async Task<HostAdminApiResponse?> ReconcileAsync(
        SipAccount account,
        CancellationToken cancellationToken)
    {
        if (reconciler is null)
        {
            return null;
        }

        var result = await reconciler.ApplyAsync(account, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return null;
        }

        account.ReportStatus(SipAccountStatus.Failed, result.Error, timeProvider.GetUtcNow());
        await store.UpdateAsync(account, cancellationToken).ConfigureAwait(false);

        return new HostAdminApiResponse(502, new
        {
            error = result.Error,
            account = SipAccountResponse.FromDomain(account)
        });
    }

    /// <summary>
    /// Removes the account from the runtime. Deprovisioning is local teardown — deregister the
    /// channel, stop new calls — so it cannot fail in a way the caller could act on.
    /// </summary>
    public Task RemoveAsync(string workspaceKey, string accountId, CancellationToken cancellationToken) =>
        reconciler is null
            ? Task.CompletedTask
            : reconciler.RemoveAsync(workspaceKey, accountId, cancellationToken);
}
