using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Application.Accounts;

/// <summary>
/// One-time migration of SIP accounts from the legacy jsonb plugin data
/// store into the plugin's EF database (PLAT-260). Runs at activation.
/// Per account it writes to EF first, then removes the jsonb copy, so an
/// interrupted run never loses data and re-running is idempotent. The
/// per-account insert-before-delete is kept deliberately over a per-workspace
/// batch: SIP accounts are few per workspace, and the safety of not losing an
/// account on a partial failure outweighs fewer transactions. The existing
/// target ids are read once per workspace to avoid a lookup per account.
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
            if (legacyAccounts.Count == 0)
            {
                continue;
            }

            var existingIds = (await targetStore.ListAsync(workspaceKey, cancellationToken).ConfigureAwait(false))
                .Select(static account => account.SipAccountId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var account in legacyAccounts)
            {
                try
                {
                    if (!existingIds.Contains(account.SipAccountId))
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
