using Callora.Host.Backend.Application.Abstractions.Persistence;
using Callora.Host.Backend.Domain.Plugins;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

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
