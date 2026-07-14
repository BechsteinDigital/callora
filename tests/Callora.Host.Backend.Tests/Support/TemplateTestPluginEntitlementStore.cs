using Callora.Host.Backend.Application.Entitlements;
using System.Collections.Concurrent;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class TemplateTestPluginEntitlementStore : IPluginEntitlementStore
{
    private readonly ConcurrentDictionary<string, bool> _entitlements =
        new(StringComparer.OrdinalIgnoreCase);

    public ValueTask<bool> IsEntitledAsync(
        string pluginId,
        string? workspaceKey = null,
        string? tenantKey = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return ValueTask.FromResult(false);
        }

        var key = BuildKey(pluginId.Trim(), workspaceKey, tenantKey);
        if (_entitlements.TryGetValue(key, out var entitled))
        {
            return ValueTask.FromResult(entitled);
        }

        if (!string.IsNullOrWhiteSpace(tenantKey))
        {
            var tenantFallbackKey = BuildKey(pluginId.Trim(), workspaceKey: null, tenantKey);
            if (_entitlements.TryGetValue(tenantFallbackKey, out entitled))
            {
                return ValueTask.FromResult(entitled);
            }
        }

        var globalFallbackKey = BuildKey(pluginId.Trim(), workspaceKey: null, tenantKey: null);
        return ValueTask.FromResult(_entitlements.TryGetValue(globalFallbackKey, out entitled) && entitled);
    }

    public ValueTask SetEntitledAsync(
        string pluginId,
        bool isEntitled,
        string? workspaceKey = null,
        string? tenantKey = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return ValueTask.CompletedTask;
        }

        _entitlements[BuildKey(pluginId.Trim(), workspaceKey, tenantKey)] = isEntitled;
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearForPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return ValueTask.CompletedTask;
        }

        var normalizedPluginId = pluginId.Trim();
        foreach (var key in _entitlements.Keys)
        {
            if (key.EndsWith($"|{normalizedPluginId}", StringComparison.OrdinalIgnoreCase))
            {
                _entitlements.TryRemove(key, out _);
            }
        }

        return ValueTask.CompletedTask;
    }

    private static string BuildKey(
        string pluginId,
        string? workspaceKey,
        string? tenantKey)
    {
        var normalizedTenantKey = string.IsNullOrWhiteSpace(tenantKey) ? "*" : tenantKey.Trim();
        var normalizedWorkspaceKey = string.IsNullOrWhiteSpace(workspaceKey) ? "*" : workspaceKey.Trim();
        return $"{normalizedTenantKey}|{normalizedWorkspaceKey}|{pluginId}";
    }
}
