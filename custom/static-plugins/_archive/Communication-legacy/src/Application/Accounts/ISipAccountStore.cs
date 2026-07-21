namespace Callora.Plugin.Communication.Application.Accounts;

/// <summary>
/// Workspace-scoped persistence for SIP accounts.
/// </summary>
public interface ISipAccountStore
{
    Task<IReadOnlyList<SipAccountEntry>> ListAsync(string workspaceKey, CancellationToken cancellationToken = default);

    Task<SipAccountEntry?> GetAsync(string workspaceKey, string sipAccountId, CancellationToken cancellationToken = default);

    Task<SipAccountEntry> CreateAsync(string workspaceKey, UpsertSipAccountRequest request, CancellationToken cancellationToken = default);

    Task<SipAccountEntry?> UpdateAsync(string workspaceKey, string sipAccountId, UpsertSipAccountRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string workspaceKey, string sipAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all workspace keys that contain SIP accounts.
    /// </summary>
    Task<IReadOnlyList<string>> ListWorkspaceKeysAsync(CancellationToken cancellationToken = default);
}
