using Callora.Core.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Deletes host-owned plugin data documents by plugin id (PLAT-260 follow-up),
/// so uninstalling a plugin leaves no orphaned rows in the host schema.
/// </summary>
public sealed class EfPluginDataDocumentCleaner(HostPersistenceDbContext dbContext) : IPluginDataDocumentCleaner
{
    public Task<int> DeleteByPluginIdAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return Task.FromResult(0);
        }

        var normalizedPluginId = pluginId.Trim();
        return dbContext.PluginDataDocuments
            .Where(document => document.PluginId == normalizedPluginId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
