using Callora.Host.Backend.Application.Abstractions.Plugins;

namespace Callora.Host.Backend.Infrastructure.Startup;

public sealed class PluginUiAssetPublishHostedService(
    IPluginUiAssetPublisher assetPublisher,
    ILogger<PluginUiAssetPublishHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await assetPublisher.PublishAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Initial plugin UI asset publish failed at startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
