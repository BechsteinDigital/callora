using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Persistence;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Options;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Infrastructure.Startup;

/// <summary>
/// Reconstitutes runtime plugin state from the database on startup: a plugin is
/// runtime-loaded only when the DB records it as installed, and activated when
/// the DB records it as active (REV2 §3.1) — directory presence alone does nothing.
/// Activation runs in dependency order (REV2 §5.1) so a plugin's required
/// capabilities are provided before it starts.
/// </summary>
/// <remarks>
/// <para>
/// <b>Und wenn <c>AutoActivateInstalledPlugins</c> gesetzt ist, auch für den Zustand
/// <see cref="PluginInstallationState.Installed"/>.</b> Ohne das war „installiert" eine Falltür: Eine
/// gescheiterte Aktualisierung lässt die Zeile dort zurück, und danach aktivierte sie niemand mehr —
/// weder diese Phase, die nur <c>Active</c> ansieht, noch die Discovery, deren Auto-Aktivierung nur für
/// neu gefundene Plugins gilt. Das Plugin lud bei jedem Start, seine Routen antworteten mit 404, und die
/// einzige Spur war eine Warnung von vor Tagen. Ein einmaliger Aussetzer wurde so dauerhaft.
/// </para>
/// <para>
/// <see cref="PluginInstallationState.Inactive"/> bleibt ausdrücklich außen vor: Das ist die
/// Entscheidung eines Betreibers, ein Plugin abzuschalten, und sie beim nächsten Start zurückzudrehen
/// wäre schlimmer als der Fehler, den das hier behebt. <c>Installed</c> heißt „noch nie aktiviert oder
/// durch einen Fehlschlag zurückgestuft" — genau das, was die Option verspricht zu aktivieren.
/// </para>
/// </remarks>
public sealed class PluginRuntimeRehydrationHostedService(
    IServiceProvider services,
    CalloraHostingOptions hostingOptions,
    ILogger<PluginRuntimeRehydrationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var installationRepository = provider.GetRequiredService<IPluginInstallationRepository>();
        var lifecycleService = provider.GetRequiredService<IPluginLifecycleService>();
        var registryReader = provider.GetService<IPluginPackageRegistryReader>();

        var installations = (await installationRepository.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(installation => installation.State != PluginInstallationState.Uninstalled)
            .ToList();

        // Phase 1: runtime-load everything the DB records as installed. Load order is
        // irrelevant — only activation must respect capability dependencies. Track which
        // plugins are actually available (already loaded or freshly installed) so a
        // failed install drops out of activation.
        var available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var installation in installations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsLoaded(lifecycleService, installation.PluginId))
            {
                available.Add(installation.PluginId);
                continue;
            }

            var install = await lifecycleService
                .InstallAsync(
                    new InstallPluginCommand(
                        installation.AssemblyPath,
                        installation.EntryTypeName,
                        "system:runtime-rehydration"),
                    cancellationToken)
                .ConfigureAwait(false);
            if (install.IsSuccess)
            {
                available.Add(installation.PluginId);
            }
            else
            {
                logger.LogWarning(
                    "Runtime rehydration could not install plugin {PluginId} from {AssemblyPath}: {Message}",
                    installation.PluginId,
                    installation.AssemblyPath,
                    install.Message);
            }
        }

        // Phase 2: activate the desired-active plugins in dependency order (REV2 §5.1),
        // so a plugin's required capabilities are provided before it starts.
        var active = installations
            .Where(installation => ShouldActivate(installation.State)
                && available.Contains(installation.PluginId))
            .Select(installation => (installation.PluginId, installation.AssemblyPath))
            .ToList();

        foreach (var pluginId in await PluginActivationOrdering.OrderAsync(active, registryReader, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsActive(lifecycleService, pluginId))
            {
                continue;
            }

            var activate = await lifecycleService
                .ActivateAsync(
                    new PluginLifecycleCommand(pluginId, "system:runtime-rehydration", WorkspaceKey: null),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!activate.IsSuccess)
            {
                logger.LogWarning(
                    "Runtime rehydration could not activate plugin {PluginId}: {Message}",
                    pluginId,
                    activate.Message);
            }
        }
    }

    private bool ShouldActivate(PluginInstallationState state) => state switch
    {
        PluginInstallationState.Active => true,
        // Die Falltür, siehe oben. Nur mit der Option, damit ein Aufbau, der Aktivierung ausdrücklich
        // als bewusste Handlung führt, unverändert bleibt.
        PluginInstallationState.Installed => hostingOptions.AutoActivateInstalledPlugins,
        _ => false,
    };

    private static bool IsLoaded(IPluginLifecycleService lifecycleService, string pluginId)
        => lifecycleService.Plugins.Any(plugin =>
            string.Equals(plugin.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));

    private static bool IsActive(IPluginLifecycleService lifecycleService, string pluginId)
        => lifecycleService.Plugins.Any(plugin =>
            string.Equals(plugin.PluginId, pluginId, StringComparison.OrdinalIgnoreCase)
            && plugin.State == HostPluginState.Active);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
