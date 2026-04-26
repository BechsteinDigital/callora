using System.Collections.Concurrent;
using Callora.Host.Backend.Application.Abstractions;

namespace Callora.Host.Backend.Application.Policies;

public sealed class InMemoryPluginEntitlementStore : IPluginEntitlementStore
{
    private readonly ConcurrentDictionary<string, byte> _entitledPluginKeys;
    private const string DefaultWorkspaceKey = "__default__";
    private const string DefaultTenantKey = "__default_tenant__";

    public InMemoryPluginEntitlementStore(BackendHostOptions options)
    {
        _entitledPluginKeys = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        foreach (var pluginId in options.ActivationEntitledPluginIds)
        {
            if (!string.IsNullOrWhiteSpace(pluginId))
            {
                _entitledPluginKeys.TryAdd(BuildKey(pluginId, null, null), 1);
            }
        }

        foreach (var entitlement in options.ActivationTenantEntitlements)
        {
            if (string.IsNullOrWhiteSpace(entitlement.PluginId))
            {
                continue;
            }

            _entitledPluginKeys.TryAdd(BuildKey(entitlement.PluginId, entitlement.TenantId, entitlement.TenantId), 1);
        }
    }

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

        if (!string.IsNullOrWhiteSpace(workspaceKey))
        {
            return ValueTask.FromResult(_entitledPluginKeys.ContainsKey(BuildKey(pluginId, workspaceKey, tenantKey)));
        }

        if (!string.IsNullOrWhiteSpace(tenantKey))
        {
            var normalizedTenantKey = tenantKey.Trim();
            var pluginSuffix = $":{pluginId.Trim()}";
            var prefix = $"{normalizedTenantKey}|";

            foreach (var key in _entitledPluginKeys.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    key.EndsWith(pluginSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    return ValueTask.FromResult(true);
                }
            }
        }

        return ValueTask.FromResult(_entitledPluginKeys.ContainsKey(BuildKey(pluginId, null, null)));
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

        var key = BuildKey(pluginId, workspaceKey, tenantKey);

        if (isEntitled)
        {
            _entitledPluginKeys.TryAdd(key, 1);
        }
        else
        {
            _entitledPluginKeys.TryRemove(key, out _);
        }

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
        foreach (var key in _entitledPluginKeys.Keys)
        {
            if (key.EndsWith($":{normalizedPluginId}", StringComparison.OrdinalIgnoreCase))
            {
                _entitledPluginKeys.TryRemove(key, out _);
            }
        }

        return ValueTask.CompletedTask;
    }

    private static string BuildKey(string pluginId, string? workspaceKey, string? tenantKey)
    {
        var normalizedTenant = string.IsNullOrWhiteSpace(tenantKey) ? DefaultTenantKey : tenantKey.Trim();
        var normalizedWorkspace = string.IsNullOrWhiteSpace(workspaceKey) ? DefaultWorkspaceKey : workspaceKey.Trim();
        return $"{normalizedTenant}|{normalizedWorkspace}:{pluginId.Trim()}";
    }
}
