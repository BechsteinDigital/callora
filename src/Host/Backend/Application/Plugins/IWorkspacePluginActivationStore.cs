namespace Callora.Host.Backend.Application.Plugins;

/// <summary>
/// Write access to per-workspace plugin activation ("switched on here").
/// Deliberately separate from the entitlement store ("allowed to use") —
/// the two are distinct domain states (PLAT-253).
/// </summary>
public interface IWorkspacePluginActivationStore
{
    Task SetActiveAsync(
        string pluginId,
        string workspaceKey,
        string tenantKey,
        bool isActive,
        CancellationToken cancellationToken = default);
}
