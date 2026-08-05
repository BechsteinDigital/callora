using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Voice;

/// <summary>
/// Brings the live voice runtime in line with a persisted <see cref="SipAccount"/> (#110).
/// <para>
/// Persisting an account is not the same as operating it: before this port existed, create,
/// update, enable, disable and delete only wrote rows, so a new account did not register
/// until the next restart and a disabled one kept taking calls. Every successful mutation
/// now runs through here, and so does startup — one reconciler, one code path, so the two
/// cannot drift.
/// </para>
/// <para>
/// Operations are <em>idempotent</em> and state-based: callers declare the account's desired
/// state and the reconciler works out whether that means connect, reconnect or nothing at all.
/// Repeating a request is therefore safe, and concurrent requests for the same account are
/// serialized.
/// </para>
/// </summary>
public interface ISipAccountRuntimeReconciler
{
    /// <summary>
    /// Makes the runtime match <paramref name="account"/>: an enabled account ends up connected
    /// and registered with its current configuration; a disabled one ends up removed. A
    /// configuration change reconnects; an unchanged enabled account is a no-op.
    /// </summary>
    Task<SipRuntimeReconciliation> ApplyAsync(SipAccount account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the account from the runtime — deregisters the channel and stops new calls
    /// immediately. Idempotent: removing an account that was never provisioned succeeds.
    /// </summary>
    Task<SipRuntimeReconciliation> RemoveAsync(
        string workspaceKey,
        string accountId,
        CancellationToken cancellationToken = default);
}
