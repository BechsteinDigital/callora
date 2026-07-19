using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Policies;
using System.Collections.Concurrent;

namespace Callora.Core.Application.Entitlements;

public sealed class InMemoryPluginEntitlementStore : IPluginEntitlementStore
{
    // Keyed by the composite scope key (see BuildKey); the value is the readable
    // projection returned by ListAsync. Presence of a key == entitled (this double
    // does not model explicit "not entitled" rows — a revoke removes the entry).
    private readonly ConcurrentDictionary<string, PluginEntitlementSnapshot> _entitledPluginKeys;
    private const string DefaultWorkspaceKey = "__default__";
    private const string DefaultTenantKey = "__default_tenant__";

    public InMemoryPluginEntitlementStore(BackendHostOptions options)
    {
        _entitledPluginKeys = new ConcurrentDictionary<string, PluginEntitlementSnapshot>(StringComparer.OrdinalIgnoreCase);
        var nowUtc = DateTimeOffset.UtcNow;

        foreach (var pluginId in options.ActivationEntitledPluginIds)
        {
            if (!string.IsNullOrWhiteSpace(pluginId))
            {
                _entitledPluginKeys.TryAdd(
                    BuildKey(pluginId, null, null),
                    new PluginEntitlementSnapshot(pluginId.Trim(), null, null, true, "config", nowUtc, nowUtc));
            }
        }

        foreach (var entitlement in options.ActivationTenantEntitlements)
        {
            if (string.IsNullOrWhiteSpace(entitlement.PluginId))
            {
                continue;
            }

            // Key preserves the historical scope encoding (tenant id in both slots)
            // so IsEntitledAsync's scan is unchanged; the snapshot records the
            // semantic tenant scope for listing.
            _entitledPluginKeys.TryAdd(
                BuildKey(entitlement.PluginId, entitlement.TenantId, entitlement.TenantId),
                new PluginEntitlementSnapshot(entitlement.PluginId.Trim(), null, entitlement.TenantId?.Trim(), true, "config", nowUtc, nowUtc));
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
        string source = "manual",
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
            var nowUtc = DateTimeOffset.UtcNow;
            var normalizedWorkspaceKey = string.IsNullOrWhiteSpace(workspaceKey) ? null : workspaceKey.Trim();
            var normalizedTenantKey = string.IsNullOrWhiteSpace(tenantKey) ? null : tenantKey.Trim();
            _entitledPluginKeys[key] = new PluginEntitlementSnapshot(
                pluginId.Trim(), normalizedWorkspaceKey, normalizedTenantKey, true, source, nowUtc, nowUtc);
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

    public ValueTask<IReadOnlyList<PluginEntitlementSnapshot>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<PluginEntitlementSnapshot> snapshot = _entitledPluginKeys.Values
            .OrderBy(x => x.PluginId, StringComparer.Ordinal)
            .ThenBy(x => x.TenantKey, StringComparer.Ordinal)
            .ThenBy(x => x.WorkspaceKey, StringComparer.Ordinal)
            .ToList();
        return ValueTask.FromResult(snapshot);
    }

    private static string BuildKey(string pluginId, string? workspaceKey, string? tenantKey)
    {
        var normalizedTenant = string.IsNullOrWhiteSpace(tenantKey) ? DefaultTenantKey : tenantKey.Trim();
        var normalizedWorkspace = string.IsNullOrWhiteSpace(workspaceKey) ? DefaultWorkspaceKey : workspaceKey.Trim();
        return $"{normalizedTenant}|{normalizedWorkspace}:{pluginId.Trim()}";
    }
}
