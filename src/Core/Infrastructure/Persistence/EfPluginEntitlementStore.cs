using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Policies;
using Callora.Core.Domain.Entitlements;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Entitlement decisions in plugin_entitlements — separate from workspace
/// activation (PLAT-253). Resolution precedence: workspace row > tenant
/// row > platform row > configured default
/// (<see cref="BackendHostOptions.DefaultPluginEntitlement"/>).
/// </summary>
public sealed class EfPluginEntitlementStore(
    HostPersistenceDbContext dbContext,
    BackendHostOptions options) : IPluginEntitlementStore
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

        var rows = await dbContext.PluginEntitlements
            .AsNoTracking()
            .Where(x => x.PluginId == normalizedPluginId)
            .Where(x =>
                (x.WorkspaceKey == null && x.TenantKey == null) ||
                (normalizedTenantKey != null && x.WorkspaceKey == null && x.TenantKey == normalizedTenantKey) ||
                (normalizedWorkspaceKey != null && x.WorkspaceKey == normalizedWorkspaceKey))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var workspaceRow = normalizedWorkspaceKey is null
            ? null
            : rows.FirstOrDefault(x => string.Equals(x.WorkspaceKey, normalizedWorkspaceKey, StringComparison.OrdinalIgnoreCase));
        if (workspaceRow is not null)
        {
            return workspaceRow.IsEntitled;
        }

        var tenantRow = normalizedTenantKey is null
            ? null
            : rows.FirstOrDefault(x =>
                x.WorkspaceKey is null &&
                string.Equals(x.TenantKey, normalizedTenantKey, StringComparison.OrdinalIgnoreCase));
        if (tenantRow is not null)
        {
            return tenantRow.IsEntitled;
        }

        var platformRow = rows.FirstOrDefault(x => x.WorkspaceKey is null && x.TenantKey is null);
        return platformRow?.IsEntitled ?? options.DefaultPluginEntitlement;
    }

    public async ValueTask SetEntitledAsync(
        string pluginId,
        bool isEntitled,
        string? workspaceKey = null,
        string? tenantKey = null,
        string source = "manual",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return;
        }

        var normalizedPluginId = pluginId.Trim();
        var normalizedWorkspaceKey = string.IsNullOrWhiteSpace(workspaceKey) ? null : workspaceKey.Trim();
        var normalizedTenantKey = string.IsNullOrWhiteSpace(tenantKey) ? null : tenantKey.Trim();
        var nowUtc = DateTimeOffset.UtcNow;

        var row = await dbContext.PluginEntitlements
            .SingleOrDefaultAsync(
                x => x.PluginId == normalizedPluginId &&
                     x.TenantKey == normalizedTenantKey &&
                     x.WorkspaceKey == normalizedWorkspaceKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            dbContext.PluginEntitlements.Add(new PluginEntitlement
            {
                Id = Guid.NewGuid(),
                PluginId = normalizedPluginId,
                TenantKey = normalizedTenantKey,
                WorkspaceKey = normalizedWorkspaceKey,
                IsEntitled = isEntitled,
                Source = source,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            });
        }
        else
        {
            // Last writer wins on provenance: an operator override of a marketplace
            // grant (or vice-versa) records who set it last.
            row.IsEntitled = isEntitled;
            row.Source = source;
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
        await dbContext.PluginEntitlements
            .Where(x => x.PluginId == normalizedPluginId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<PluginEntitlementSnapshot>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.PluginEntitlements
            .AsNoTracking()
            .OrderBy(x => x.PluginId)
            .ThenBy(x => x.TenantKey)
            .ThenBy(x => x.WorkspaceKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(x => new PluginEntitlementSnapshot(
                x.PluginId,
                x.WorkspaceKey,
                x.TenantKey,
                x.IsEntitled,
                x.Source,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToList();
    }
}
