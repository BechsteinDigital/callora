using Callora.Host.Backend.Application.Abstractions.Plugins;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class EfWorkspacePluginActivationReader(HostPersistenceDbContext dbContext) : IWorkspacePluginActivationReader
{
    public async Task<IReadOnlyList<string>> ListActivePluginIdsAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
            return [];

        var normalizedKey = workspaceKey.Trim();
        return await dbContext.WorkspacePluginActivations
            .AsNoTracking()
            .Where(activation => activation.WorkspaceKey == normalizedKey && activation.IsActive)
            .Select(activation => activation.PluginId)
            .Distinct()
            .OrderBy(pluginId => pluginId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
