namespace Callora.Host.Backend.Application.Abstractions;

public interface IPluginEntitlementStore
{
    ValueTask<bool> IsEntitledAsync(
        string pluginId,
        string? workspaceKey = null,
        string? tenantKey = null,
        CancellationToken cancellationToken = default);

    ValueTask SetEntitledAsync(
        string pluginId,
        bool isEntitled,
        string? workspaceKey = null,
        string? tenantKey = null,
        CancellationToken cancellationToken = default);

    ValueTask ClearForPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default);
}
