using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Accounts;

/// <summary>Workspace-scoped persistence port for <see cref="SipAccount"/>.</summary>
public interface ISipAccountStore
{
    /// <summary>Lists all accounts of a workspace.</summary>
    Task<IReadOnlyList<SipAccount>> ListAsync(string workspaceKey, CancellationToken cancellationToken = default);

    /// <summary>Gets one account, or null.</summary>
    Task<SipAccount?> GetAsync(string workspaceKey, string accountId, CancellationToken cancellationToken = default);

    /// <summary>Persists a new account.</summary>
    Task AddAsync(SipAccount account, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing account.</summary>
    Task UpdateAsync(SipAccount account, CancellationToken cancellationToken = default);

    /// <summary>Deletes an account; returns false when it did not exist.</summary>
    Task<bool> DeleteAsync(string workspaceKey, string accountId, CancellationToken cancellationToken = default);
}
