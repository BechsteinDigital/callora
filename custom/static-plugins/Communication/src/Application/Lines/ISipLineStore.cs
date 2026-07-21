using Callora.Plugin.Communication.Domain.Lines;

namespace Callora.Plugin.Communication.Application.Lines;

/// <summary>Workspace-scoped persistence port for <see cref="SipLine"/>.</summary>
public interface ISipLineStore
{
    /// <summary>Lists the lines of one account.</summary>
    Task<IReadOnlyList<SipLine>> ListByAccountAsync(string accountId, CancellationToken cancellationToken = default);

    /// <summary>Gets one line, or null.</summary>
    Task<SipLine?> GetAsync(string workspaceKey, string lineId, CancellationToken cancellationToken = default);

    /// <summary>Persists a new line.</summary>
    Task AddAsync(SipLine line, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing line.</summary>
    Task UpdateAsync(SipLine line, CancellationToken cancellationToken = default);

    /// <summary>Deletes a line; returns false when it did not exist.</summary>
    Task<bool> DeleteAsync(string workspaceKey, string lineId, CancellationToken cancellationToken = default);

    /// <summary>Deletes all lines of a workspace (GDPR workspace purge). Returns the count.</summary>
    Task<int> DeleteByWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Number of lines in a workspace — the seam a Cloud entitlement gate reads before
    /// creating a line (self-hosted stays unlimited; the limit source lives outside the plugin).
    /// </summary>
    Task<int> CountByWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default);
}
