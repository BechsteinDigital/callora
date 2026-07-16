using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Options;
using Callora.Core.Application.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Infrastructure.Startup;

/// <summary>
/// Runs local plugin discovery once at startup when <see cref="CalloraHostingOptions.AutoLoadPlugins"/>
/// is enabled — a thin caller of <see cref="IPluginDiscoveryService"/>. On-demand refresh
/// (admin overview, CLI) uses the same service directly.
/// </summary>
public sealed class LocalPluginDiscoveryHostedService(
    IServiceProvider services,
    CalloraHostingOptions hostingOptions,
    ILogger<LocalPluginDiscoveryHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!hostingOptions.AutoLoadPlugins)
        {
            return;
        }

        using var scope = services.CreateScope();
        var discovery = scope.ServiceProvider.GetRequiredService<IPluginDiscoveryService>();
        var result = await discovery.RefreshAsync(cancellationToken).ConfigureAwait(false);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Plugin discovery: {Added} added, {Updated} updated, {Removed} removed, {MissingActive} missing-active.",
                result.Added.Count,
                result.Updated.Count,
                result.RemovedInactive.Count,
                result.MissingActive.Count);
        }

        // Preserve the historical auto-activation of freshly discovered plugins when
        // configured; otherwise activation stays a deliberate, DB-driven action.
        if (hostingOptions.AutoActivateInstalledPlugins && result.Added.Count > 0)
        {
            var lifecycleService = scope.ServiceProvider.GetRequiredService<IPluginLifecycleService>();
            foreach (var pluginId in result.Added)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var activate = await lifecycleService
                    .ActivateAsync(new PluginLifecycleCommand(pluginId, "system:startup-discovery", WorkspaceKey: null), cancellationToken)
                    .ConfigureAwait(false);
                if (!activate.IsSuccess)
                {
                    logger.LogWarning("Auto-activation after discovery failed for {PluginId}: {Message}", pluginId, activate.Message);
                }
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
