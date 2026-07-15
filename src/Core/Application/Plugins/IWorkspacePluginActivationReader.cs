namespace Callora.Core.Application.Plugins;

/// <summary>
/// Read access to per-workspace plugin activation state.
/// </summary>
public interface IWorkspacePluginActivationReader
{
    Task<IReadOnlyList<string>> ListActivePluginIdsAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default);
}
