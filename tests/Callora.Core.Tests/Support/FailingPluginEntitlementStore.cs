
using Callora.Core.Application.Entitlements;

namespace Callora.Core.Tests.Support;

internal sealed class FailingPluginEntitlementStore : IPluginEntitlementStore
{
    public ValueTask<bool> IsEntitledAsync(
        string pluginId,
        string? workspaceKey = null,
        string? tenantKey = null,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<bool>(new InvalidOperationException("entitlement store down"));

    public ValueTask SetEntitledAsync(
        string pluginId,
        bool isEntitled,
        string? workspaceKey = null,
        string? tenantKey = null,
        CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public ValueTask ClearForPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
