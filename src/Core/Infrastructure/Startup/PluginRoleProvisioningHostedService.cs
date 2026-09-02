using Callora.Core.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Infrastructure.Startup;

/// <summary>
/// Legt beim Start die Rollen der Plugins an, die schon installiert sind.
/// </summary>
/// <remarks>
/// <para>
/// Der Ereignis-Weg (<c>PluginRoleSyncSubscriber</c>) fängt jede künftige Installation ab. Er fängt
/// nicht ab, was vorher da war — und das ist auf jeder bestehenden Installation alles. Ohne diesen
/// Durchgang bekäme ein Betreiber seine Rollen erst, wenn er jedes Plugin einmal neu installiert.
/// </para>
/// <para>
/// <b>Nach der Rehydrierung registriert, nicht davor.</b> Ein Plugin liefert seine Berechtigungen
/// entweder im Manifest oder über einen Contributor, und der zweite Weg existiert erst, wenn das Plugin
/// geladen ist. Liefe das hier vorher, bekämen genau die Plugins keine Rolle, die den Contributor-Weg
/// benutzen — wortlos, weil eine leere Schlüsselliste von „hat keine Berechtigungen" nicht zu
/// unterscheiden ist.
/// </para>
/// </remarks>
public sealed class PluginRoleProvisioningHostedService(
    IServiceProvider services,
    ILogger<PluginRoleProvisioningHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = services.CreateScope();
            var provisioner = scope.ServiceProvider.GetRequiredService<PluginRoleProvisioner>();
            var dbContext = scope.ServiceProvider.GetRequiredService<HostPersistenceDbContext>();

            await provisioner.ProvisionAsync(dbContext, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Ein Host, der wegen einer fehlenden Rolle nicht startet, ist die schlechtere Antwort:
            // Ohne die Rolle fehlt eine Bequemlichkeit, ohne den Host fehlt alles. Der Super-Admin
            // kommt in jedem Fall an jede Oberfläche.
            logger.LogWarning(ex, "Die Plugin-Rollen konnten beim Start nicht angelegt werden.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
