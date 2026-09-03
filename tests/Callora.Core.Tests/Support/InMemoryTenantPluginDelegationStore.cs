using Callora.Core.Application.Plugins;
using System.Collections.Concurrent;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Test double for the tenant's delegation decision. Absence means "not delegated" — the same
/// fail-closed default the real store has.
/// </summary>
public sealed class InMemoryTenantPluginDelegationStore : ITenantPluginDelegationStore
{
    private readonly ConcurrentDictionary<(string Tenant, string Plugin), bool> _entries =
        new();

    public Task<bool> MayWorkspacesAssignAsync(
        string tenantKey,
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(pluginId))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(
            _entries.TryGetValue((tenantKey.Trim(), pluginId.Trim()), out var allowed) && allowed);
    }

    public Task<IReadOnlyList<string>> ListDelegatedAsync(
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var trimmed = tenantKey?.Trim() ?? string.Empty;
        return Task.FromResult<IReadOnlyList<string>>(
            [.. _entries
                .Where(entry => entry.Value &&
                                string.Equals(entry.Key.Tenant, trimmed, StringComparison.Ordinal))
                .Select(entry => entry.Key.Plugin)
                .Order(StringComparer.Ordinal)]);
    }

    public Task SetAsync(
        string tenantKey,
        string pluginId,
        bool workspacesMayAssign,
        string? updatedBy,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries[(tenantKey.Trim(), pluginId.Trim())] = workspacesMayAssign;
        return Task.CompletedTask;
    }
}
