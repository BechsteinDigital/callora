using Callora.Core.Application.Events;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Infrastructure.Events;

/// <summary>
/// Legt die Rolle eines Plugins an, sobald es installiert, aktualisiert oder aktiviert wurde.
/// </summary>
/// <remarks>
/// <para>
/// <b>Auch bei Aktivierung, nicht nur bei Installation.</b> Ein Plugin, das seine Berechtigungen über
/// einen Contributor beisteuert, ist zum Zeitpunkt der Installation noch nicht geladen — seine Schlüssel
/// gibt es dann schlicht nicht. Erst das Aktivieren lädt es. Beide Ereignisse abzufangen ist billig,
/// weil das Anlegen ohnehin nur einmal passiert.
/// </para>
/// <para>
/// <b>Deinstallation räumt nichts weg.</b> Die Rolle kann Benutzern zugewiesen sein, und ein Benutzer
/// hat genau eine — sie zu löschen nähme ihnen jeden Zugang, auch den, der mit dem Plugin nichts zu tun
/// hat. Eine Rolle ohne Plugin ist eine Rolle, deren Berechtigungen ins Leere greifen; das ist
/// harmlos und sichtbar, das andere wäre keins von beidem.
/// </para>
/// </remarks>
public sealed class PluginRoleSyncSubscriber(
    PluginRoleProvisioner provisioner,
    HostPersistenceDbContext dbContext,
    ILogger<PluginRoleSyncSubscriber> logger) : IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>
{
    public async Task HandleAsync(
        PluginLifecycleChangedEvent appEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appEvent);

        if (!appEvent.IsSuccess || string.IsNullOrWhiteSpace(appEvent.PluginId))
        {
            return;
        }

        var action = appEvent.Action?.Trim();
        if (action is not (PluginLifecycleActions.Install
            or PluginLifecycleActions.Update
            or PluginLifecycleActions.Activate))
        {
            return;
        }

        try
        {
            await provisioner.ProvisionAsync(dbContext, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Eine fehlgeschlagene Rollenanlage darf die Installation nicht scheitern lassen: Das
            // Plugin ist da und funktioniert, es fehlt eine Bequemlichkeit. Der nächste Start
            // versucht es erneut.
            logger.LogWarning(
                ex, "Die Rolle für Plugin {PluginId} konnte nach '{Action}' nicht angelegt werden.",
                appEvent.PluginId, action);
        }
    }
}
