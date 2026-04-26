namespace Callora.Host.Backend.Application.Abstractions.Plugins;

/// <summary>
/// Publishes plugin-provided frontend assets into the host webroot.
/// </summary>
public interface IPluginUiAssetPublisher
{
    Task PublishAllAsync(CancellationToken cancellationToken = default);
}
