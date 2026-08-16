using Callora.Core.Application.Persistence;
using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class EfPluginInstallationRepository(
    HostPersistenceDbContext dbContext,
    IPluginAssemblyPathPortability pathPortability) : IPluginInstallationRepository
{
    public async Task<IReadOnlyList<PluginInstallation>> ListAsync(CancellationToken cancellationToken = default)
    {
        var installations = await dbContext.PluginInstallations
            .AsNoTracking()
            .OrderBy(x => x.PluginId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return installations.Select(Resolve).ToArray();
    }

    public async Task<PluginInstallation?> GetByPluginIdAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        var installation = await dbContext.PluginInstallations
            .SingleOrDefaultAsync(x => x.PluginId == pluginId, cancellationToken)
            .ConfigureAwait(false);

        return installation is null ? null : Resolve(installation);
    }

    public Task AddAsync(
        PluginInstallation installation,
        CancellationToken cancellationToken = default) =>
        dbContext.PluginInstallations.AddAsync(installation, cancellationToken).AsTask();

    // Der eine Ort, an dem aus dem gespeicherten Pfad ein Dateipfad wird: Kein anderer Zugriff
    // geht am DbSet vorbei, und alles darüber sieht damit weiterhin einen Pfad, den es öffnen
    // kann. Die Zuweisung ändert nur einen ungemappten Wert — eine getrackte Zeile bleibt
    // dadurch unverändert und wird beim nächsten SaveChanges nicht angefasst.
    private PluginInstallation Resolve(PluginInstallation installation)
    {
        var fileSystemPath = pathPortability.ToFileSystemPath(installation.StoredAssemblyPath);
        if (!string.IsNullOrWhiteSpace(fileSystemPath))
        {
            installation.ResolveAssemblyPath(fileSystemPath);
        }

        return installation;
    }
}
