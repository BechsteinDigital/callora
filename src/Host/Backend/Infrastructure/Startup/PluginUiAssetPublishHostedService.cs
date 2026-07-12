using Callora.Host.Backend.Application.Abstractions.Plugins;

namespace Callora.Host.Backend.Infrastructure.Startup;

public sealed class PluginUiAssetPublishHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<PluginUiAssetPublishHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var assetPublisher = scope.ServiceProvider.GetRequiredService<IPluginUiAssetPublisher>();
            await assetPublisher.PublishAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Initial plugin UI asset publish failed at startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
