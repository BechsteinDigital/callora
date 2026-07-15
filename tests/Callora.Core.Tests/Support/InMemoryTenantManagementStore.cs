using System.Collections.Concurrent;
using Callora.Core.Application.Tenants;

namespace Callora.Core.Tests.Support;

internal sealed class InMemoryTenantManagementStore : ITenantManagementStore
{
    private readonly ConcurrentDictionary<string, TenantSnapshot> _tenants = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<TenantSnapshot>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<TenantSnapshot>>(
            _tenants.Values.OrderBy(x => x.TenantKey, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public Task<TenantSnapshot?> GetAsync(
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return Task.FromResult<TenantSnapshot?>(null);
        }

        _tenants.TryGetValue(tenantKey.Trim(), out var tenant);
        return Task.FromResult(tenant);
    }

    public Task<TenantCreateResult> CreateAsync(
        string tenantKey,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var normalizedTenantKey = tenantKey.Trim();
        if (_tenants.ContainsKey(normalizedTenantKey))
        {
            return Task.FromResult(new TenantCreateResult(TenantCreateStatus.AlreadyExists));
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var tenant = new TenantSnapshot(
            normalizedTenantKey,
            displayName.Trim(),
            true,
            nowUtc,
            nowUtc);
        _tenants[normalizedTenantKey] = tenant;

        return Task.FromResult(new TenantCreateResult(TenantCreateStatus.Created, tenant));
    }

    public Task<TenantSetStateResult> SetActiveStateAsync(
        string tenantKey,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(tenantKey) || !_tenants.TryGetValue(tenantKey.Trim(), out var tenant))
        {
            return Task.FromResult(new TenantSetStateResult(TenantSetStateStatus.NotFound));
        }

        var updated = tenant with { IsActive = isActive, UpdatedAtUtc = DateTimeOffset.UtcNow };
        _tenants[tenant.TenantKey] = updated;

        return Task.FromResult(new TenantSetStateResult(TenantSetStateStatus.Updated, updated));
    }

    public Task<TenantDeleteResult> RemoveAsync(
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return Task.FromResult(new TenantDeleteResult(TenantDeleteStatus.NotFound));
        }

        return Task.FromResult(_tenants.TryRemove(tenantKey.Trim(), out _)
            ? new TenantDeleteResult(TenantDeleteStatus.Deleted)
            : new TenantDeleteResult(TenantDeleteStatus.NotFound));
    }
}
