namespace Callora.Plugins.Dialer.Application.Numbers;

/// <summary>
/// Workspace-scoped persistence for dial list numbers.
/// </summary>
public interface IDialNumberStore
{
    Task<IReadOnlyList<DialNumberEntry>> ListAsync(string workspaceKey, CancellationToken cancellationToken = default);

    Task<DialNumberEntry> AddAsync(string workspaceKey, string number, string? displayName, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(string workspaceKey, string numberId, CancellationToken cancellationToken = default);
}
