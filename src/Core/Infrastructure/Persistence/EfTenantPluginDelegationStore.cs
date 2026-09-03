using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class EfTenantPluginDelegationStore(HostPersistenceDbContext dbContext)
    : ITenantPluginDelegationStore
{
    public async Task<bool> MayWorkspacesAssignAsync(
        string tenantKey,
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(pluginId))
        {
            return false;
        }

        var normalizedTenant = tenantKey.Trim();
        var normalizedPlugin = pluginId.Trim();

        // IgnoreQueryFilters: Die Frage wird für eine WORKSPACE-Sitzung gestellt, und die sieht die
        // Tabelle des Mandanten nicht. Ohne das antwortete der Filter statt der Daten — immer nein,
        // und die Delegation bliebe wirkungslos, ohne dass irgendwo etwas fehlschlägt. Der Mandant
        // steht im Where, also verlässt die Antwort seinen Bereich nicht.
        return await dbContext.TenantPluginDelegations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                x => x.TenantKey == normalizedTenant &&
                     x.PluginId == normalizedPlugin &&
                     x.WorkspacesMayAssign,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListDelegatedAsync(
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return [];
        }

        var normalizedTenant = tenantKey.Trim();
        return await dbContext.TenantPluginDelegations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantKey == normalizedTenant && x.WorkspacesMayAssign)
            .Select(x => x.PluginId)
            .OrderBy(x => x)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SetAsync(
        string tenantKey,
        string pluginId,
        bool workspacesMayAssign,
        string? updatedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var normalizedTenant = tenantKey.Trim();
        var normalizedPlugin = pluginId.Trim();

        var existing = await dbContext.TenantPluginDelegations
            .FirstOrDefaultAsync(
                x => x.TenantKey == normalizedTenant && x.PluginId == normalizedPlugin,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            dbContext.TenantPluginDelegations.Add(new TenantPluginDelegation
            {
                Id = Guid.NewGuid(),
                TenantKey = normalizedTenant,
                PluginId = normalizedPlugin,
                WorkspacesMayAssign = workspacesMayAssign,
                UpdatedBy = updatedBy,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            existing.WorkspacesMayAssign = workspacesMayAssign;
            existing.UpdatedBy = updatedBy;
            existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
