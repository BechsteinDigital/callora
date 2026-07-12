using Callora.Host.Backend.Application.Abstractions.Persistence;
using Callora.Host.Backend.Application.Lifecycle;
using Callora.Host.Backend.Domain.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Callora.Host.PluginContracts.Application.Plugins;

namespace Callora.Host.Backend.Infrastructure.Startup;

public sealed class PluginRuntimeRehydrationHostedService(
    IServiceProvider services,
    ILogger<PluginRuntimeRehydrationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var installationRepository = scope.ServiceProvider.GetRequiredService<IPluginInstallationRepository>();
        var lifecycleService = scope.ServiceProvider.GetRequiredService<IPluginLifecycleService>();
        var installations = await installationRepository.ListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var installation in installations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (installation.State == PluginInstallationState.Uninstalled)
            {
                continue;
            }

            var runtimeDescriptor = lifecycleService.Plugins.FirstOrDefault(x =>
                string.Equals(x.PluginId, installation.PluginId, StringComparison.OrdinalIgnoreCase));
            if (runtimeDescriptor is null)
            {
                var install = await lifecycleService
                    .InstallAsync(
                        new InstallPluginCommand(
                            installation.AssemblyPath,
                            installation.EntryTypeName,
                            "system:runtime-rehydration"),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!install.IsSuccess)
                {
                    logger.LogWarning(
                        "Runtime rehydration could not install plugin {PluginId} from {AssemblyPath}: {Message}",
                        installation.PluginId,
                        installation.AssemblyPath,
                        install.Message);
                    continue;
                }

                runtimeDescriptor = lifecycleService.Plugins.FirstOrDefault(x =>
                    string.Equals(x.PluginId, installation.PluginId, StringComparison.OrdinalIgnoreCase));
            }

            if (installation.State != PluginInstallationState.Active)
            {
                continue;
            }

            if (runtimeDescriptor is not null && runtimeDescriptor.State == HostPluginState.Active)
            {
                continue;
            }

            var activate = await lifecycleService
                .ActivateAsync(
                    new PluginLifecycleCommand(
                        installation.PluginId,
                        RequestedBy: "system:runtime-rehydration",
                        WorkspaceKey: null),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!activate.IsSuccess)
            {
                logger.LogWarning(
                    "Runtime rehydration could not activate plugin {PluginId}: {Message}",
                    installation.PluginId,
                    activate.Message);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
