using Callora.Core.Application.Events;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Snippets;
using Callora.Core.Infrastructure.Snippets;

namespace Callora.Core.Infrastructure.Events;

/// <summary>
/// Hält die Snippet-Basis eines Pakets über Installieren, Aktualisieren und Deinstallieren hinweg
/// aktuell (ADR-024) — dasselbe Muster wie beim Konfigurationsschema und den Custom Fields.
/// </summary>
public sealed class PluginSnippetSyncSubscriber(
    RegistrySnippetSyncService syncService,
    ISnippetCache cache,
    ILogger<PluginSnippetSyncSubscriber> logger) : IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>
{
    public async Task HandleAsync(PluginLifecycleChangedEvent appEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appEvent);

        if (!appEvent.IsSuccess || string.IsNullOrWhiteSpace(appEvent.PluginId))
        {
            return;
        }

        var action = appEvent.Action?.Trim();
        var pluginId = appEvent.PluginId.Trim();

        try
        {
            switch (action)
            {
                case PluginLifecycleActions.Install:
                case PluginLifecycleActions.Update:
                    if (appEvent.Metadata is not null &&
                        appEvent.Metadata.TryGetValue("assemblyPath", out var assemblyPath) &&
                        !string.IsNullOrWhiteSpace(assemblyPath))
                    {
                        await syncService
                            .SyncFromAssemblyAsync(pluginId, ResolveVersion(appEvent.Metadata), assemblyPath, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    break;
                case PluginLifecycleActions.Uninstall:
                    await syncService.ClearPluginSnippetsAsync(pluginId, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    return;
            }

            // Die Basis liegt unter jeder Kette: Ein installiertes, aktualisiertes oder entferntes
            // Paket ändert damit jedes aufgelöste Wörterbuch, nicht nur eines.
            cache.InvalidateAll();
        }
        catch (Exception ex)
        {
            // Wie bei den Geschwister-Abonnenten: Ein Fehler beim Einlesen der Texte darf einen
            // Lebenszyklus-Schritt nicht scheitern lassen — das Plugin läuft dann mit den
            // Schlüsseln statt mit den Texten, und das steht hier.
            logger.LogWarning(ex, "Snippet sync failed for plugin {PluginId} on action {Action}.", pluginId, action);
        }
    }

    private static string ResolveVersion(IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata.TryGetValue("registryVersion", out var registryVersion) && !string.IsNullOrWhiteSpace(registryVersion))
        {
            return registryVersion.Trim();
        }

        return metadata.TryGetValue("packageVersion", out var packageVersion) && !string.IsNullOrWhiteSpace(packageVersion)
            ? packageVersion.Trim()
            : "0.0.0";
    }
}
