using Callora.Core.Application.Tenants;
using Callora.Core.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class EfTenantManagementStore(HostPersistenceDbContext dbContext) : ITenantManagementStore
{
    public async Task<IReadOnlyList<TenantSnapshot>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Tenants
            .AsNoTracking()
            .OrderBy(x => x.TenantKey)
            .Select(ToSnapshotExpression())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<TenantSnapshot?> GetAsync(
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return Task.FromResult<TenantSnapshot?>(null);
        }

        var normalizedTenantKey = tenantKey.Trim();
        return dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.TenantKey == normalizedTenantKey)
            .Select(ToSnapshotExpression())
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<TenantCreateResult> CreateAsync(
        string tenantKey,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var normalizedTenantKey = tenantKey.Trim();
        var existing = await dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(x => x.TenantKey == normalizedTenantKey, cancellationToken)
            .ConfigureAwait(false);
        if (existing)
        {
            return new TenantCreateResult(TenantCreateStatus.AlreadyExists);
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            TenantKey = normalizedTenantKey,
            DisplayName = displayName.Trim(),
            IsActive = true,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };

        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TenantCreateResult(TenantCreateStatus.Created, ToSnapshot(tenant));
    }

    public async Task<TenantSetStateResult> SetActiveStateAsync(
        string tenantKey,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return new TenantSetStateResult(TenantSetStateStatus.NotFound);
        }

        var normalizedTenantKey = tenantKey.Trim();
        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.TenantKey == normalizedTenantKey, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            return new TenantSetStateResult(TenantSetStateStatus.NotFound);
        }

        tenant.IsActive = isActive;
        tenant.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TenantSetStateResult(TenantSetStateStatus.Updated, ToSnapshot(tenant));
    }

    public async Task<TenantDeleteResult> RemoveAsync(
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return new TenantDeleteResult(TenantDeleteStatus.NotFound);
        }

        var normalizedTenantKey = tenantKey.Trim();
        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.TenantKey == normalizedTenantKey, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            return new TenantDeleteResult(TenantDeleteStatus.NotFound);
        }

        var hasWorkspaces = await dbContext.Workspaces
            .AsNoTracking()
            .AnyAsync(x => x.TenantId == tenant.Id, cancellationToken)
            .ConfigureAwait(false);
        if (hasWorkspaces)
        {
            return new TenantDeleteResult(TenantDeleteStatus.HasWorkspaces);
        }

        dbContext.Tenants.Remove(tenant);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TenantDeleteResult(TenantDeleteStatus.Deleted);
    }

    private static TenantSnapshot ToSnapshot(Tenant tenant)
    {
        return new TenantSnapshot(
            tenant.TenantKey,
            tenant.DisplayName,
            tenant.IsActive,
            tenant.CreatedAtUtc,
            tenant.UpdatedAtUtc);
    }

    private static Expression<Func<Tenant, TenantSnapshot>> ToSnapshotExpression()
    {
        return x => new TenantSnapshot(
            x.TenantKey,
            x.DisplayName,
            x.IsActive,
            x.CreatedAtUtc,
            x.UpdatedAtUtc);
    }
}
