using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Domain.Plugins;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class EfPluginEntitlementStore(HostPersistenceDbContext dbContext) : IPluginEntitlementStore
{
    public async ValueTask<bool> IsEntitledAsync(
        string pluginId,
        string? workspaceKey = null,
        string? tenantKey = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return false;
        }

        var normalizedPluginId = pluginId.Trim();
        var normalizedWorkspaceKey = string.IsNullOrWhiteSpace(workspaceKey) ? null : workspaceKey.Trim();
        var normalizedTenantKey = string.IsNullOrWhiteSpace(tenantKey) ? null : tenantKey.Trim();

        var query = dbContext.WorkspacePluginActivations
            .AsNoTracking()
            .Where(x => x.IsActive && x.PluginId == normalizedPluginId);

        if (!string.IsNullOrWhiteSpace(normalizedWorkspaceKey))
        {
            query = query.Where(x => x.WorkspaceKey == normalizedWorkspaceKey);
        }

        if (!string.IsNullOrWhiteSpace(normalizedTenantKey))
        {
            query = query.Where(x => x.TenantKey == normalizedTenantKey);
        }

        return await query
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask SetEntitledAsync(
        string pluginId,
        bool isEntitled,
        string? workspaceKey = null,
        string? tenantKey = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pluginId) ||
            string.IsNullOrWhiteSpace(workspaceKey) ||
            string.IsNullOrWhiteSpace(tenantKey))
        {
            return;
        }

        var normalizedPluginId = pluginId.Trim();
        var normalizedWorkspaceKey = workspaceKey.Trim();
        var normalizedTenantKey = tenantKey.Trim();
        var nowUtc = DateTimeOffset.UtcNow;

        var row = await dbContext.WorkspacePluginActivations
            .SingleOrDefaultAsync(
                x => x.PluginId == normalizedPluginId &&
                     x.TenantKey == normalizedTenantKey &&
                     x.WorkspaceKey == normalizedWorkspaceKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new WorkspacePluginActivation
            {
                Id = Guid.NewGuid(),
                TenantKey = normalizedTenantKey,
                WorkspaceKey = normalizedWorkspaceKey,
                PluginId = normalizedPluginId,
                IsActive = isEntitled,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };
            dbContext.WorkspacePluginActivations.Add(row);
        }
        else
        {
            row.TenantKey = normalizedTenantKey;
            row.IsActive = isEntitled;
            row.UpdatedAtUtc = nowUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask ClearForPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return;
        }

        var normalizedPluginId = pluginId.Trim();
        await dbContext.WorkspacePluginActivations
            .Where(x => x.PluginId == normalizedPluginId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
