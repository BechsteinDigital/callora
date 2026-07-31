using Callora.Core.Application.Workspaces.Contracts;

namespace Callora.Core.Application.Workspaces;

internal sealed class ScopedWorkspaceSurfaceProvisioner(
    IServiceScopeFactory scopeFactory) : IWorkspaceSurfaceProvisioner
{
    public async Task<PluginSurfaceLocation?> EnsureAsync(
        string workspaceKey,
        PluginSurfaceDefinition definition,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<WorkspaceSurfaceProvisioner>();
        return await provisioner
            .EnsureAsync(workspaceKey, definition, cancellationToken)
            .ConfigureAwait(false);
    }
}
