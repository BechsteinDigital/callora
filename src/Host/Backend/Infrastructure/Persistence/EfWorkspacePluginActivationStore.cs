using Callora.Host.Backend.Application.Abstractions.Plugins;
using Callora.Host.Backend.Domain.Plugins;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

/// <summary>
/// Persists per-workspace plugin activation. Entitlement ("allowed to use")
/// lives in plugin_entitlements — this table only answers "switched on
/// here" (PLAT-253).
/// </summary>
public sealed class EfWorkspacePluginActivationStore(HostPersistenceDbContext dbContext)
    : IWorkspacePluginActivationStore
{
    public async Task SetActiveAsync(
        string pluginId,
        string workspaceKey,
        string tenantKey,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);

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
                IsActive = isActive,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };
            dbContext.WorkspacePluginActivations.Add(row);
        }
        else
        {
            row.IsActive = isActive;
            row.UpdatedAtUtc = nowUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
