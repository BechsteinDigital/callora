using Callora.Core.Application.Persistence;
using Callora.Core.Domain.Plugins;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class EfPluginInstallationRepository(HostPersistenceDbContext dbContext) : IPluginInstallationRepository
{
    public async Task<IReadOnlyList<PluginInstallation>> ListAsync(CancellationToken cancellationToken = default) =>
        await dbContext.PluginInstallations
            .AsNoTracking()
            .OrderBy(x => x.PluginId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<PluginInstallation?> GetByPluginIdAsync(
        string pluginId,
        CancellationToken cancellationToken = default) =>
        dbContext.PluginInstallations
            .SingleOrDefaultAsync(x => x.PluginId == pluginId, cancellationToken);

    public Task AddAsync(
        PluginInstallation installation,
        CancellationToken cancellationToken = default) =>
        dbContext.PluginInstallations.AddAsync(installation, cancellationToken).AsTask();
}
