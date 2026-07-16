using Callora.Core.Application.Persistence;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Infrastructure.Startup;

/// <summary>
/// Reconstitutes runtime plugin state from the database on startup: a plugin is
/// runtime-loaded only when the DB records it as installed, and activated only when
/// the DB records it as active (REV2 §3.1) — directory presence alone does nothing.
/// Activation runs in dependency order (REV2 §5.1) so a plugin's required
/// capabilities are provided before it starts.
/// </summary>
public sealed class PluginRuntimeRehydrationHostedService(
    IServiceProvider services,
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
            .Where(installation => installation.State == PluginInstallationState.Active
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

    private static bool IsLoaded(IPluginLifecycleService lifecycleService, string pluginId)
        => lifecycleService.Plugins.Any(plugin =>
            string.Equals(plugin.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));

    private static bool IsActive(IPluginLifecycleService lifecycleService, string pluginId)
        => lifecycleService.Plugins.Any(plugin =>
            string.Equals(plugin.PluginId, pluginId, StringComparison.OrdinalIgnoreCase)
            && plugin.State == HostPluginState.Active);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
