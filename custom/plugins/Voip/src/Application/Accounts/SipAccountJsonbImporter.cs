using Microsoft.Extensions.Logging;

namespace Callora.Plugins.Voip.Application.Accounts;

/// <summary>
/// One-time migration of SIP accounts from the legacy jsonb plugin data
/// store into the plugin's EF database (PLAT-260). Runs at activation:
/// per account it writes to EF first, then removes the jsonb copy, so an
/// interrupted run never loses data and re-running is idempotent (an already
/// imported account is skipped, an already deleted jsonb copy is a no-op).
/// </summary>
public sealed class SipAccountJsonbImporter(
    ISipAccountStore legacyStore,
    ISipAccountStore targetStore,
    ILogger? logger = null)
{
    public async Task ImportAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> workspaceKeys;
        try
        {
            workspaceKeys = await legacyStore.ListWorkspaceKeysAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "Reading legacy SIP account workspaces failed; skipping import.");
            return;
        }

        var imported = 0;
        foreach (var workspaceKey in workspaceKeys)
        {
            var legacyAccounts = await legacyStore.ListAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            foreach (var account in legacyAccounts)
            {
                try
                {
                    if (await targetStore.GetAsync(workspaceKey, account.SipAccountId, cancellationToken).ConfigureAwait(false) is null)
                    {
                        await targetStore.CreateAsync(
                                workspaceKey,
                                new UpsertSipAccountRequest(account.Username, account.Domain, account.DisplayName, account.Secret, account.IsActive),
                                cancellationToken)
                            .ConfigureAwait(false);
                        imported++;
                    }

                    await legacyStore.DeleteAsync(workspaceKey, account.SipAccountId, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    logger?.LogWarning(
                        exception,
                        "Importing SIP account {AccountId} of workspace {WorkspaceKey} failed; legacy copy retained.",
                        account.SipAccountId,
                        workspaceKey);
                }
            }
        }

        if (imported > 0)
        {
            logger?.LogInformation("Imported {Count} SIP accounts from jsonb into the plugin database.", imported);
        }
    }
}
