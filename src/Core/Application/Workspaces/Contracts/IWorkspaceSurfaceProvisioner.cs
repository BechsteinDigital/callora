namespace Callora.Core.Application.Workspaces.Contracts;

/// <summary>
/// Published host contract through which a plugin idempotently provisions its
/// own public workspace surface without gaining access to host persistence.
/// </summary>
public interface IWorkspaceSurfaceProvisioner
{
    /// <summary>
    /// Creates or reconciles the declared surface and returns its resolved
    /// public route, or null when the workspace does not exist.
    /// </summary>
    Task<PluginSurfaceLocation?> EnsureAsync(
        string workspaceKey,
        PluginSurfaceDefinition definition,
        CancellationToken cancellationToken = default);
}
