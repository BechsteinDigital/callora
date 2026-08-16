using Callora.Core.Application.Persistence;
using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Infrastructure.Startup;

/// <summary>
/// Bringt gespeicherte Plugin-Pfade auf die portable Form, bevor irgendetwas sie zu laden versucht
/// (#307).
/// </summary>
/// <remarks>
/// Zwei Fälle, beide aus der Zeit vor dem Umbau. Ein Pfad, der absolut dasteht und unter einer
/// aktuellen Wurzel liegt, wird relativ zu ihr geschrieben — damit der nächste Umgebungswechsel ihn
/// nicht mehr trifft. Ein Pfad, dessen Umgebung es hier nicht gibt, wird unter den aktuellen
/// Wurzeln gesucht; wird dieselbe Datei gefunden, zeigt die Zeile danach auf sie. Das ist genau der
/// Eingriff, den ein Betreiber sonst als <c>UPDATE … replace(…)</c> von Hand fahren muss — nur dass
/// er hier nachweislich auf eine vorhandene Datei zeigt und im Log steht.
///
/// Wird nichts gefunden, bleibt die Zeile, wie sie ist: Ein Pfad, den niemand auflösen kann, ist
/// ein Befund und keine Gelegenheit zum Raten. Die Rehydration meldet ihn danach wie bisher.
/// </remarks>
internal sealed class PluginAssemblyPathNormalizationHostedService(
    IServiceProvider services,
    ILogger<PluginAssemblyPathNormalizationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var installations = provider.GetRequiredService<IPluginInstallationRepository>();
        var portability = provider.GetRequiredService<IPluginAssemblyPathPortability>();
        var unitOfWork = provider.GetRequiredService<IHostUnitOfWork>();

        var changed = 0;
        foreach (var installation in await installations.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (installation.State == PluginInstallationState.Uninstalled)
            {
                continue;
            }

            // Die Zeile aus ListAsync ist ungetrackt; geschrieben wird über die getrackte.
            var tracked = await installations.GetByPluginIdAsync(installation.PluginId, cancellationToken).ConfigureAwait(false);
            if (tracked is null || !TryNormalize(portability, tracked, out var storedPath))
            {
                continue;
            }

            tracked.RelocateAssembly(storedPath, DateTimeOffset.UtcNow);
            changed++;
        }

        if (changed > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private bool TryNormalize(
        IPluginAssemblyPathPortability portability,
        PluginInstallation installation,
        out string storedPath)
    {
        storedPath = string.Empty;
        var stored = installation.StoredAssemblyPath;
        var fileSystemPath = portability.ToFileSystemPath(stored);

        if (File.Exists(fileSystemPath))
        {
            var portable = portability.ToStoredPath(fileSystemPath);
            if (string.Equals(portable, stored, StringComparison.Ordinal))
            {
                return false;
            }

            logger.LogInformation(
                "Plugin {PluginId}: stored assembly path is now relative to its plugin root ({StoredPath}).",
                installation.PluginId,
                portable);
            storedPath = portable;
            return true;
        }

        if (!portability.TryLocateInRoots(stored, out var located))
        {
            return false;
        }

        // Auf Warn-Ebene, weil hier etwas repariert wird, das ohne Eingriff eine fehlende
        // Oberfläche gewesen wäre — wer das im Log sieht, soll wissen, woher der Pfad kam.
        logger.LogWarning(
            "Plugin {PluginId}: the stored assembly path {StoredPath} does not exist here; found the same file at {LocatedPath} and updated the installation.",
            installation.PluginId,
            stored,
            located);
        storedPath = portability.ToStoredPath(located);
        return true;
    }
}
